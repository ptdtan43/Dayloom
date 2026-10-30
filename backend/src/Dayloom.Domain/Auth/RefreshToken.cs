namespace Dayloom.Domain.Auth;

/// <summary>
/// A single refresh token in a session's rotation chain. The raw token only ever lives in the user's cookie;
/// the database stores its SHA-256 hash, so a database leak does not hand out working tokens.
/// </summary>
public class RefreshToken
{
    public const int HashLengthInBytes = 32;

    // Arrays are mutable even behind a private setter, so the hash is copied on the way in and on the way out.
    private byte[] _tokenHash = [];

    // Required by EF Core to materialize rows; tokens are created through Session.IssueRefreshToken().
    private RefreshToken() { }

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }

    /// <summary>A copy of the hash: changing the returned array does not change the token.</summary>
    public byte[] TokenHash => (byte[])_tokenHash.Clone();

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Set when the token is exchanged. Presenting it again afterwards means it was reused.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    public bool IsUsed => UsedAt is not null;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public void MarkUsed(DateTimeOffset now)
    {
        if (IsUsed)
        {
            throw new InvalidOperationException("A refresh token can only be used once.");
        }

        if (now < CreatedAt || IsExpired(now))
        {
            throw new ArgumentOutOfRangeException(nameof(now), now, "A refresh token can only be used between its creation and expiry.");
        }

        UsedAt = now.ToUniversalTime();
    }

    internal static RefreshToken Create(Guid sessionId, byte[] tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        if (tokenHash.Length != HashLengthInBytes)
        {
            throw new ArgumentException($"Token hash must be {HashLengthInBytes} bytes (SHA-256).", nameof(tokenHash));
        }

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("A refresh token must expire after it is created.", nameof(expiresAt));
        }

        return new RefreshToken
        {
            Id = Guid.CreateVersion7(createdAt),
            SessionId = sessionId,
            _tokenHash = (byte[])tokenHash.Clone(),
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
        };
    }
}
