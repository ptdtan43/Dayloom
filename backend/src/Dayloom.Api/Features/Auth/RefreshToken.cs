using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dayloom.Api.Features.Auth;

/// <summary>
/// A single refresh token in a session's rotation chain. The raw token only ever lives in the user's cookie;
/// the database stores its SHA-256 hash, so a database leak does not hand out working tokens.
/// </summary>
public class RefreshToken
{
    public const int HashLengthInBytes = 32;

    public Guid Id { get; set; }

    public Guid SessionId { get; set; }
    public Session Session { get; set; } = null!;

    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set when the token is exchanged. Presenting it again afterwards means it was reused.</summary>
    public DateTimeOffset? UsedAt { get; set; }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> token)
    {
        token.HasIndex(t => t.TokenHash).IsUnique();

        token.HasOne(t => t.Session)
            .WithMany(s => s.RefreshTokens)
            .HasForeignKey(t => t.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        token.ToTable(t =>
        {
            t.HasCheckConstraint("ck_refresh_tokens_hash_length", $"octet_length(token_hash) = {RefreshToken.HashLengthInBytes}");
            t.HasCheckConstraint("ck_refresh_tokens_expires_after_created", "expires_at > created_at");
        });
    }
}
