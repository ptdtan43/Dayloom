using Dayloom.Domain.Auth;
using Dayloom.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dayloom.Infrastructure.Persistence.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> session)
    {
        // Domain only knows UserId; the foreign key to the Identity user table is a persistence detail.
        session.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The public property hands out a read-only wrapper; EF Core loads and tracks tokens through the private list.
        session.Navigation(s => s.RefreshTokens)
            .HasField("_refreshTokens")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        session.ToTable(t =>
        {
            t.HasCheckConstraint("ck_sessions_expires_after_created", "absolute_expires_at > created_at");

            // Expressed in hours, not days: adding '14 days' to a timestamptz follows calendar days and can
            // come out as 335 or 337 hours across a DST change. The limit is an absolute duration.
            t.HasCheckConstraint(
                "ck_sessions_max_lifetime",
                $"absolute_expires_at <= created_at + interval '{(int)Session.MaxLifetime.TotalHours} hours'");
        });
    }
}
