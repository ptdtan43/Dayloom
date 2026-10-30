using System.Security.Cryptography;
using Dayloom.Domain.Auth;

namespace Dayloom.UnitTests.Domain.Auth;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_token_is_unused()
    {
        var token = NewToken();

        Assert.False(token.IsUsed);
        Assert.Null(token.UsedAt);
    }

    [Fact]
    public void Mark_used_records_the_time()
    {
        var token = NewToken();

        token.MarkUsed(Now.AddMinutes(1));

        Assert.True(token.IsUsed);
        Assert.Equal(Now.AddMinutes(1), token.UsedAt);
    }

    [Fact]
    public void A_token_cannot_be_used_twice()
    {
        var token = NewToken();
        token.MarkUsed(Now);

        Assert.Throws<InvalidOperationException>(() => token.MarkUsed(Now.AddMinutes(1)));
    }

    [Fact]
    public void Is_expired_from_its_expiry_time_onwards()
    {
        var token = NewToken();

        Assert.False(token.IsExpired(token.ExpiresAt.AddTicks(-1)));
        Assert.True(token.IsExpired(token.ExpiresAt));
    }

    private static RefreshToken NewToken() =>
        Session.Start(Guid.NewGuid(), Now).IssueRefreshToken(RandomNumberGenerator.GetBytes(RefreshToken.HashLengthInBytes), Now);
}
