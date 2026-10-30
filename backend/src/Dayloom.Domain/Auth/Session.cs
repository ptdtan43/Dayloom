namespace Dayloom.Domain.Auth;

/// <summary>
/// One login = one session = one family of rotating refresh tokens (docs/DESIGN.md §2).
/// Revoking the session kills every refresh token in it.
/// </summary>
public class Session
{
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromDays(14);

    private readonly List<RefreshToken> _refreshTokens = [];

    // Required by EF Core to materialize rows; application code goes through Start().
    private Session() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Hard limit (login + 14 days). Rotation never extends it.</summary>
    public DateTimeOffset AbsoluteExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// A read-only wrapper, not the list itself: casting it back to a list cannot add or remove tokens.
    /// Token history must survive, because a reused old token is how token theft is detected.
    /// </summary>
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    public static Session Start(Guid userId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A session must belong to a user.", nameof(userId));
        }

        now = now.ToUniversalTime();
        return new Session
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            CreatedAt = now,
            AbsoluteExpiresAt = now + MaxLifetime,
        };
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now >= CreatedAt && now < AbsoluteExpiresAt;

    /// <summary>Idempotent: revoking twice keeps the first revocation time.</summary>
    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        if (now < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(now), now, "A session cannot be revoked before it started.");
        }

        RevokedAt = now.ToUniversalTime();
    }

    /// <summary>
    /// Adds a refresh token to this session. The token expires together with the session, which the database
    /// cannot enforce on its own (a CHECK constraint cannot look at another table).
    /// </summary>
    public RefreshToken IssueRefreshToken(byte[] tokenHash, DateTimeOffset now)
    {
        if (!IsActive(now))
        {
            throw new InvalidOperationException("Cannot issue a refresh token outside the session's active period (revoked, expired or not started).");
        }

        var token = RefreshToken.Create(Id, tokenHash, createdAt: now.ToUniversalTime(), expiresAt: AbsoluteExpiresAt);
        _refreshTokens.Add(token);
        return token;
    }
}
