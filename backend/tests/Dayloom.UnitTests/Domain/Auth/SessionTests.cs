using System.Security.Cryptography;
using Dayloom.Domain.Auth;

namespace Dayloom.UnitTests.Domain.Auth;

public class SessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 2, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Start_sets_an_absolute_expiry_of_14_days()
    {
        var session = Session.Start(UserId, Now);

        Assert.Equal(UserId, session.UserId);
        Assert.Equal(Now, session.CreatedAt);
        Assert.Equal(Now.AddDays(14), session.AbsoluteExpiresAt);
        Assert.Null(session.RevokedAt);
        Assert.NotEqual(Guid.Empty, session.Id);
    }

    [Fact]
    public void Start_stores_times_in_utc()
    {
        var vietnamTime = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.FromHours(7));

        var session = Session.Start(UserId, vietnamTime);

        Assert.Equal(TimeSpan.Zero, session.CreatedAt.Offset);
        Assert.Equal(Now, session.CreatedAt);
    }

    [Fact]
    public void Start_requires_a_user()
    {
        Assert.Throws<ArgumentException>(() => Session.Start(Guid.Empty, Now));
    }

    [Fact]
    public void Is_active_until_the_absolute_expiry()
    {
        var session = Session.Start(UserId, Now);

        Assert.True(session.IsActive(Now));
        Assert.True(session.IsActive(session.AbsoluteExpiresAt.AddTicks(-1)));
        Assert.False(session.IsActive(session.AbsoluteExpiresAt));
    }

    [Fact]
    public void Revoke_deactivates_the_session_and_keeps_the_first_revocation_time()
    {
        var session = Session.Start(UserId, Now);

        session.Revoke(Now.AddMinutes(5));
        session.Revoke(Now.AddMinutes(10));

        Assert.False(session.IsActive(Now.AddMinutes(6)));
        Assert.Equal(Now.AddMinutes(5), session.RevokedAt);
    }

    [Fact]
    public void Issued_refresh_token_expires_with_the_session_not_later()
    {
        var session = Session.Start(UserId, Now);

        var token = session.IssueRefreshToken(NewHash(), Now.AddDays(3));

        Assert.Equal(session.Id, token.SessionId);
        Assert.Equal(Now.AddDays(3), token.CreatedAt);
        Assert.Equal(session.AbsoluteExpiresAt, token.ExpiresAt);
        Assert.Contains(token, session.RefreshTokens);
    }

    [Fact]
    public void Cannot_issue_a_refresh_token_for_a_revoked_session()
    {
        var session = Session.Start(UserId, Now);
        session.Revoke(Now);

        Assert.Throws<InvalidOperationException>(() => session.IssueRefreshToken(NewHash(), Now));
    }

    [Fact]
    public void Cannot_issue_a_refresh_token_for_an_expired_session()
    {
        var session = Session.Start(UserId, Now);

        Assert.Throws<InvalidOperationException>(() => session.IssueRefreshToken(NewHash(), session.AbsoluteExpiresAt));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    public void Rejects_a_token_hash_that_is_not_sha256_sized(int length)
    {
        var session = Session.Start(UserId, Now);

        Assert.Throws<ArgumentException>(() => session.IssueRefreshToken(RandomNumberGenerator.GetBytes(length), Now));
    }

    private static byte[] NewHash() => RandomNumberGenerator.GetBytes(RefreshToken.HashLengthInBytes);
}
