using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dayloom.Api.Features.Auth;

/// <summary>
/// One login = one session = one family of rotating refresh tokens (docs/DESIGN.md §2).
/// Revoking the session kills every refresh token in it.
/// </summary>
public class Session
{
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromDays(14);

    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Hard limit (login + 14 days). Rotation never extends it.</summary>
    public DateTimeOffset AbsoluteExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public List<RefreshToken> RefreshTokens { get; } = [];
}

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> session)
    {
        session.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

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
