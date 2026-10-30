using Dayloom.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dayloom.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> token)
    {
        // The public getter returns a copy; EF Core reads and writes the private array directly.
        token.Property(t => t.TokenHash)
            .HasField("_tokenHash")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        token.HasIndex(t => t.TokenHash).IsUnique();

        token.HasOne<Session>()
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
