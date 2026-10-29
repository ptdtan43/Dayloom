using System.Security.Cryptography;
using Dayloom.Api.Features.Auth;
using Dayloom.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Dayloom.Api.Tests.Features.Auth;

/// <summary>
/// Guards the database-level rules from docs/DESIGN.md §2. These must hold even if application code has a bug,
/// so they are tested against PostgreSQL directly rather than through the API.
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
        await using var db = factory.CreateDbContext();
        db.Users.AddRange(NewUser("same@example.com"), NewUser("same@example.com"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Rejects_a_duplicate_refresh_token_hash()
    {
        await using var db = factory.CreateDbContext();
        var session = NewSession(NewUser($"{Guid.NewGuid()}@example.com"));
        var hash = RandomNumberGenerator.GetBytes(RefreshToken.HashLengthInBytes);
        session.RefreshTokens.Add(NewToken(hash));
        session.RefreshTokens.Add(NewToken(hash));
        db.Sessions.Add(session);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Rejects_a_refresh_token_hash_of_the_wrong_length()
    {
        await using var db = factory.CreateDbContext();
        var session = NewSession(NewUser($"{Guid.NewGuid()}@example.com"));
        session.RefreshTokens.Add(NewToken(RandomNumberGenerator.GetBytes(16)));
        db.Sessions.Add(session);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Rejects_a_session_that_expires_before_it_starts()
    {
        await using var db = factory.CreateDbContext();
        var session = NewSession(NewUser($"{Guid.NewGuid()}@example.com"));
        session.AbsoluteExpiresAt = session.CreatedAt.AddMinutes(-1);
        db.Sessions.Add(session);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Deleting_a_user_removes_their_sessions_and_tokens()
    {
        var user = NewUser($"{Guid.NewGuid()}@example.com");
        var session = NewSession(user);
        session.RefreshTokens.Add(NewToken(RandomNumberGenerator.GetBytes(RefreshToken.HashLengthInBytes)));
        await using (var db = factory.CreateDbContext())
        {
            db.Sessions.Add(session);
            await db.SaveChangesAsync();
        }

        await using (var db = factory.CreateDbContext())
        {
            await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();

            Assert.False(await db.Sessions.AnyAsync(s => s.Id == session.Id));
            Assert.False(await db.RefreshTokens.AnyAsync(t => t.SessionId == session.Id));
        }
    }

    private static AppUser NewUser(string email) => new()
    {
        Id = Guid.NewGuid(),
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        CreatedAt = Now,
    };

    private static Session NewSession(AppUser user) => new()
    {
        User = user,
        CreatedAt = Now,
        AbsoluteExpiresAt = Now.AddDays(14),
    };

    private static RefreshToken NewToken(byte[] hash) => new()
    {
        TokenHash = hash,
        CreatedAt = Now,
        ExpiresAt = Now.AddDays(14),
    };
}
