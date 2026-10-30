using System.Security.Cryptography;
using Dayloom.Api.Features.Auth;
using Dayloom.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dayloom.Api.Tests.Features.Auth;

/// <summary>
/// Guards the database-level rules from docs/DESIGN.md §2. These must hold even if application code has a bug,
/// so they are tested against PostgreSQL directly rather than through the API.
/// Every rejection test names the exact constraint it expects, so a different failure (another index, a missing FK)
/// cannot make it pass by accident.
/// </summary>
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
        var user = NewUser($"{Guid.NewGuid()}@example.com");
        if (missingColumn == nameof(AppUser.Email)) user.Email = null;
        else user.NormalizedEmail = null;

        var error = await AssertRejectedAsync(PostgresErrorCodes.NotNullViolation, constraint: null, db => db.Users.Add(user));

        Assert.Equal(missingColumn == nameof(AppUser.Email) ? "email" : "normalized_email", error.ColumnName);
    }

    [Fact]
    public async Task Accepts_valid_users_with_different_emails()
    {
        await using var db = factory.CreateDbContext();
        db.Users.AddRange(NewUser($"{Guid.NewGuid()}@example.com"), NewUser($"{Guid.NewGuid()}@example.com"));

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Rejects_a_duplicate_refresh_token_hash()
    {
        var session = NewSession();
        var hash = NewHash();
        session.RefreshTokens.Add(NewToken(hash));
        session.RefreshTokens.Add(NewToken(hash));

        await AssertRejectedAsync(PostgresErrorCodes.UniqueViolation, "ix_refresh_tokens_token_hash", db => db.Sessions.Add(session));
    }

    [Fact]
    public async Task Rejects_a_refresh_token_hash_of_the_wrong_length()
    {
        var session = NewSession();
        session.RefreshTokens.Add(NewToken(RandomNumberGenerator.GetBytes(16)));

        await AssertRejectedAsync(PostgresErrorCodes.CheckViolation, "ck_refresh_tokens_hash_length", db => db.Sessions.Add(session));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Rejects_a_refresh_token_that_expires_at_or_before_creation(int minutesAfterCreation)
    {
        var session = NewSession();
        var token = NewToken(NewHash());
        token.ExpiresAt = token.CreatedAt.AddMinutes(minutesAfterCreation);
        session.RefreshTokens.Add(token);

        await AssertRejectedAsync(PostgresErrorCodes.CheckViolation, "ck_refresh_tokens_expires_after_created", db => db.Sessions.Add(session));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Rejects_a_session_that_expires_at_or_before_creation(int minutesAfterCreation)
    {
        var session = NewSession();
        session.AbsoluteExpiresAt = session.CreatedAt.AddMinutes(minutesAfterCreation);

        await AssertRejectedAsync(PostgresErrorCodes.CheckViolation, "ck_sessions_expires_after_created", db => db.Sessions.Add(session));
    }

    [Fact]
    public async Task Accepts_a_session_of_exactly_the_maximum_lifetime()
    {
        await using var db = factory.CreateDbContext();
        var session = NewSession();
        session.AbsoluteExpiresAt = session.CreatedAt + Session.MaxLifetime;
        db.Sessions.Add(session);

        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData("00:00:00.000001")] // one microsecond: the smallest step a PostgreSQL timestamp can store
    [InlineData("1.00:00:00")]
    public async Task Rejects_a_session_longer_than_the_maximum_lifetime(string extra)
    {
        var session = NewSession();
        session.AbsoluteExpiresAt = session.CreatedAt + Session.MaxLifetime + TimeSpan.Parse(extra);

        await AssertRejectedAsync(PostgresErrorCodes.CheckViolation, "ck_sessions_max_lifetime", db => db.Sessions.Add(session));
    }

    [Fact]
    public async Task Deleting_a_user_removes_their_sessions_and_tokens()
    {
        var session = NewSession();
        session.RefreshTokens.Add(NewToken(NewHash()));
        await using (var db = factory.CreateDbContext())
        {
            db.Sessions.Add(session);
            await db.SaveChangesAsync();
        }

        await using (var db = factory.CreateDbContext())
        {
            await db.Users.Where(u => u.Id == session.User.Id).ExecuteDeleteAsync();

            Assert.False(await db.Sessions.AnyAsync(s => s.Id == session.Id));
            Assert.False(await db.RefreshTokens.AnyAsync(t => t.SessionId == session.Id));
        }
    }

    /// <summary>Saves the change and asserts PostgreSQL rejected it with exactly this error code and constraint.</summary>
    private async Task<PostgresException> AssertRejectedAsync(string sqlState, string? constraint, Action<Dayloom.Api.Data.AppDbContext> arrange)
    {
        await using var db = factory.CreateDbContext();
        arrange(db);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(sqlState, postgres.SqlState);
        Assert.Equal(constraint, postgres.ConstraintName);
        return postgres;
    }

    private static byte[] NewHash() => RandomNumberGenerator.GetBytes(RefreshToken.HashLengthInBytes);

    private static AppUser NewUser(string email, string? userName = null) => new()
    {
        Id = Guid.NewGuid(),
        UserName = userName ?? email,
        NormalizedUserName = (userName ?? email).ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        CreatedAt = Now,
    };

    private static Session NewSession() => new()
    {
        User = NewUser($"{Guid.NewGuid()}@example.com"),
        CreatedAt = Now,
        AbsoluteExpiresAt = Now + Session.MaxLifetime,
    };

    private static RefreshToken NewToken(byte[] hash) => new()
    {
        TokenHash = hash,
        CreatedAt = Now,
        ExpiresAt = Now + Session.MaxLifetime,
    };
}
