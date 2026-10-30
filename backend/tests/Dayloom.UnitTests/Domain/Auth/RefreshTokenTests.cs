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
    public void Can_be_used_from_creation_until_just_before_expiry()
    {
        var atCreation = NewToken();
        var justBeforeExpiry = NewToken();

        atCreation.MarkUsed(atCreation.CreatedAt);
        justBeforeExpiry.MarkUsed(justBeforeExpiry.ExpiresAt.AddTicks(-1));

        Assert.True(atCreation.IsUsed);
        Assert.True(justBeforeExpiry.IsUsed);
    }

    [Fact]
    public void Cannot_be_used_before_creation_and_state_is_unchanged()
    {
        var token = NewToken();

        Assert.Throws<ArgumentOutOfRangeException>(() => token.MarkUsed(token.CreatedAt.AddTicks(-1)));
        Assert.False(token.IsUsed);
    }

    [Fact]
    public void Cannot_be_used_once_expired_and_state_is_unchanged()
    {
        var token = NewToken();

        Assert.Throws<ArgumentOutOfRangeException>(() => token.MarkUsed(token.ExpiresAt));
        Assert.False(token.IsUsed);
    }

    [Fact]
    public void Is_expired_from_its_expiry_time_onwards()
    {
        var token = NewToken();

        Assert.False(token.IsExpired(token.ExpiresAt.AddTicks(-1)));
        Assert.True(token.IsExpired(token.ExpiresAt));
    }

    [Fact]
    public void Changing_the_input_array_does_not_change_the_stored_hash()
    {
        var input = NewHash();
        var original = (byte[])input.Clone();
        var token = Session.Start(Guid.NewGuid(), Now).IssueRefreshToken(input, Now);

        input[0] ^= 0xFF;

        Assert.Equal(original, token.TokenHash);
    }

    [Fact]
    public void Changing_the_returned_array_does_not_change_the_stored_hash()
    {
        var token = NewToken();
        // Copy explicitly: if the getter leaked the internal array, "original" would change along with it.
        var original = (byte[])token.TokenHash.Clone();

        token.TokenHash[0] ^= 0xFF;

        Assert.Equal(original, token.TokenHash);
    }

    private static byte[] NewHash() => RandomNumberGenerator.GetBytes(RefreshToken.HashLengthInBytes);

    private static RefreshToken NewToken() => Session.Start(Guid.NewGuid(), Now).IssueRefreshToken(NewHash(), Now);
}
