using System.Security.Cryptography;
using Dayloom.Domain.Auth;
using Dayloom.Infrastructure.Identity;
using Dayloom.Infrastructure.Persistence;
using Dayloom.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dayloom.IntegrationTests.Persistence;

/// <summary>
/// Guards the database-level rules from docs/DESIGN.md §2. These must hold even if application code has a bug,
/// so they are tested against PostgreSQL directly rather than through the API.
/// Every rejection test names the exact constraint it expects, so a different failure (another index, a missing FK)
/// cannot make it pass by accident.
/// </summary>
/// <remarks>
/// The domain model already refuses most invalid values, so these tests deliberately go around it
/// (<c>db.Entry(...).Property(...).CurrentValue</c>) to prove the database is a second, independent line of defense.
/// </remarks>
public class AuthSchemaTests(DayloomApiFactory factory) : IClassFixture<DayloomApiFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Migrations_are_in_sync_with_the_model()
    {
        await using var db = factory.CreateDbContext();

        Assert.False(db.Database.HasPendingModelChanges(), "Model changed without a migration: run 'dotnet ef migrations add'.");
    }

    [Fact]
    public async Task Rejects_two_users_with_the_same_email()
    {
        var email = $"{Guid.NewGuid()}@example.com";
        // Different user names, so only the email index can be the one that rejects this.
        var first = NewUser(email, userName: "first-" + email);
        var second = NewUser(email, userName: "second-" + email);

        await AssertRejectedAsync(PostgresErrorCodes.UniqueViolation, "ix_users_normalized_email", db => db.Users.AddRange(first, second));
    }

    [Theory]
    [InlineData(nameof(AppUser.Email))]
    [InlineData(nameof(AppUser.NormalizedEmail))]
    public async Task Rejects_a_user_without_email(string missingColumn)
    {
        var user = NewUser();
        if (missingColumn == nameof(AppUser.Email)) user.Email = null;
        else user.NormalizedEmail = null;

        var error = await AssertRejectedAsync(PostgresErrorCodes.NotNullViolation, constraint: null, db => db.Users.Add(user));

        Assert.Equal(missingColumn == nameof(AppUser.Email) ? "email" : "normalized_email", error.ColumnName);
    }

    [Fact]
    public async Task Accepts_valid_users_with_different_emails()
    {
        await using var db = factory.CreateDbContext();
        db.Users.AddRange(NewUser(), NewUser());

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Accepts_a_session_and_refresh_token_created_through_the_domain()
    {
        var user = NewUser();
        var session = Session.Start(user.Id, Now);
        session.IssueRefreshToken(NewHash(), Now);
        await using var db = factory.CreateDbContext();
        db.Users.Add(user);
        db.Sessions.Add(session);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Rejects_a_duplicate_refresh_token_hash()
    {
        var user = NewUser();
        var session = Session.Start(user.Id, Now);
        var hash = NewHash();
        session.IssueRefreshToken(hash, Now);
        session.IssueRefreshToken(hash, Now);

        await AssertRejectedAsync(PostgresErrorCodes.UniqueViolation, "ix_refresh_tokens_token_hash", db => Add(db, user, session));
    }

    [Fact]
    public async Task Rejects_a_refresh_token_hash_of_the_wrong_length()
    {
        var user = NewUser();
        var session = Session.Start(user.Id, Now);
        var token = session.IssueRefreshToken(NewHash(), Now);

        await AssertRejectedAsync(PostgresErrorCodes.CheckViolation, "ck_refresh_tokens_hash_length", db =>
        {
            Add(db, user, session);
            db.Entry(token).Property(t => t.TokenHash).CurrentValue = RandomNumberGenerator.GetBytes(16);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Rejects_a_refresh_token_that_expires_at_or_before_creation(int minutesAfterCreation)
    {
        var user = NewUser();
        var session = Session.Start(user.Id, Now);
        var token = session.IssueRefreshToken(NewHash(), Now);

        await AssertRejectedAsync(PostgresErrorCodes.CheckViolation, "ck_refresh_tokens_expires_after_created", db =>
        {
            Add(db, user, session);
            db.Entry(token).Property(t => t.ExpiresAt).CurrentValue = Now.AddMinutes(minutesAfterCreation);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Rejects_a_session_that_expires_at_or_before_creation(int minutesAfterCreation)
    {
        var user = NewUser();
        var session = Session.Start(user.Id, Now);

        await AssertRejectedAsync(PostgresErrorCodes.CheckViolation, "ck_sessions_expires_after_created", db =>
        {
            Add(db, user, session);
            db.Entry(session).Property(s => s.AbsoluteExpiresAt).CurrentValue = Now.AddMinutes(minutesAfterCreation);
        });
    }

    [Fact]
    public async Task Accepts_a_session_of_exactly_the_maximum_lifetime()
    {
        var user = NewUser();
        var session = Session.Start(user.Id, Now);
        Assert.Equal(Now + Session.MaxLifetime, session.AbsoluteExpiresAt);
        await using var db = factory.CreateDbContext();
        Add(db, user, session);

        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData("00:00:00.000001")] // one microsecond: the smallest step a PostgreSQL timestamp can store
    [InlineData("1.00:00:00")]
    public async Task Rejects_a_session_longer_than_the_maximum_lifetime(string extra)
    {
        var user = NewUser();
        var session = Session.Start(user.Id, Now);

        await AssertRejectedAsync(PostgresErrorCodes.CheckViolation, "ck_sessions_max_lifetime", db =>
        {
            Add(db, user, session);
            db.Entry(session).Property(s => s.AbsoluteExpiresAt).CurrentValue = Now + Session.MaxLifetime + TimeSpan.Parse(extra);
        });
    }

    [Fact]
    public async Task Deleting_a_user_removes_their_sessions_and_tokens()
    {
        var user = NewUser();
        var session = Session.Start(user.Id, Now);
        session.IssueRefreshToken(NewHash(), Now);
        await using (var db = factory.CreateDbContext())
        {
            Add(db, user, session);
            await db.SaveChangesAsync();
        }

        await using (var db = factory.CreateDbContext())
        {
            await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();

            Assert.False(await db.Sessions.AnyAsync(s => s.Id == session.Id));
            Assert.False(await db.RefreshTokens.AnyAsync(t => t.SessionId == session.Id));
        }
    }

    /// <summary>Saves the change and asserts PostgreSQL rejected it with exactly this error code and constraint.</summary>
    private async Task<PostgresException> AssertRejectedAsync(string sqlState, string? constraint, Action<AppDbContext> arrange)
    {
        await using var db = factory.CreateDbContext();
        arrange(db);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(sqlState, postgres.SqlState);
        Assert.Equal(constraint, postgres.ConstraintName);
        return postgres;
    }

    private static void Add(AppDbContext db, AppUser user, Session session)
    {
        db.Users.Add(user);
        db.Sessions.Add(session);
    }

    private static byte[] NewHash() => RandomNumberGenerator.GetBytes(RefreshToken.HashLengthInBytes);

    private static AppUser NewUser(string? email = null, string? userName = null)
    {
        email ??= $"{Guid.NewGuid()}@example.com";
        userName ??= email;
        return new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            CreatedAt = Now,
        };
    }
}
