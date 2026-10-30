using System.Security.Cryptography;
using Dayloom.Domain.Auth;
using Dayloom.Infrastructure.Identity;
using Dayloom.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Dayloom.IntegrationTests.Persistence;

/// <summary>
/// The domain hides its state (private setters, a copied hash, a read-only token list). These tests prove EF Core
/// still saves and loads all of it, and that the read-only surface really protects data that is already stored.
/// </summary>
public class AuthPersistenceTests(DayloomApiFactory factory) : IClassFixture<DayloomApiFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Session_and_tokens_round_trip_with_all_private_state()
    {
        var (user, session) = NewSessionForNewUser();
        var usedHash = NewHash();
        var currentHash = NewHash();
        var used = session.IssueRefreshToken(usedHash, Now);
        used.MarkUsed(Now.AddMinutes(10));
        session.IssueRefreshToken(currentHash, Now.AddMinutes(10));
        session.Revoke(Now.AddMinutes(20));
        await SaveAsync(user, session);

        await using var db = factory.CreateDbContext();
        var loaded = await db.Sessions.Include(s => s.RefreshTokens).SingleAsync(s => s.Id == session.Id);

        Assert.Equal(session.UserId, loaded.UserId);
        Assert.Equal(session.CreatedAt, loaded.CreatedAt);
        Assert.Equal(session.AbsoluteExpiresAt, loaded.AbsoluteExpiresAt);
        Assert.Equal(Now.AddMinutes(20), loaded.RevokedAt);
        Assert.Equal(2, loaded.RefreshTokens.Count);
        var loadedUsed = Assert.Single(loaded.RefreshTokens, t => t.Id == used.Id);
        Assert.Equal(usedHash, loadedUsed.TokenHash);
        Assert.Equal(Now.AddMinutes(10), loadedUsed.UsedAt);
        Assert.Equal(session.AbsoluteExpiresAt, loadedUsed.ExpiresAt);
    }

    [Fact]
    public async Task A_token_can_be_found_by_its_hash()
    {
        var (user, session) = NewSessionForNewUser();
        var hash = NewHash();
        var token = session.IssueRefreshToken(hash, Now);
        await SaveAsync(user, session);

        await using var db = factory.CreateDbContext();
        var found = await db.RefreshTokens.SingleAsync(t => t.TokenHash == hash);

        Assert.Equal(token.Id, found.Id);
    }

    [Fact]
    public async Task Stored_token_history_cannot_be_deleted_through_the_loaded_collection()
    {
        var (user, session) = NewSessionForNewUser();
        var oldToken = session.IssueRefreshToken(NewHash(), Now);
        oldToken.MarkUsed(Now.AddMinutes(1));
        session.IssueRefreshToken(NewHash(), Now.AddMinutes(1));
        await SaveAsync(user, session);

        await using (var db = factory.CreateDbContext())
        {
            var loaded = await db.Sessions.Include(s => s.RefreshTokens).SingleAsync(s => s.Id == session.Id);
            var asCollection = (ICollection<RefreshToken>)loaded.RefreshTokens;

            Assert.Throws<NotSupportedException>(() => asCollection.Remove(asCollection.First(t => t.Id == oldToken.Id)));
            await db.SaveChangesAsync();
        }

        await using (var db = factory.CreateDbContext())
        {
            Assert.Equal(2, await db.RefreshTokens.CountAsync(t => t.SessionId == session.Id));
        }
    }

    private async Task SaveAsync(AppUser user, Session session)
    {
        await using var db = factory.CreateDbContext();
        db.Users.Add(user);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
    }

    private static (AppUser User, Session Session) NewSessionForNewUser()
    {
        var email = $"{Guid.NewGuid()}@example.com";
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            CreatedAt = Now,
        };
        return (user, Session.Start(user.Id, Now));
    }

    private static byte[] NewHash() => RandomNumberGenerator.GetBytes(RefreshToken.HashLengthInBytes);
}
