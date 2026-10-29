using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dayloom.Api.Features.Auth;

/// <summary>
/// Identity handles email, password hashing and lockout; we only add what Dayloom needs on top.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public const string DefaultTimeZoneId = "Asia/Ho_Chi_Minh";

    /// <summary>IANA time zone used for every date boundary shown to this user (docs/DESIGN.md §3).</summary>
    public string TimeZoneId { get; set; } = DefaultTimeZoneId;

    public DateTimeOffset CreatedAt { get; set; }
}

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> user)
    {
        user.ToTable("users");
        user.Property(u => u.TimeZoneId).HasMaxLength(64);

        // Identity only checks email uniqueness in code, which two concurrent sign-ups can both pass.
        // The unique index makes the database the final judge (docs/DESIGN.md §1).
        user.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ix_users_normalized_email").IsUnique();
        user.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ix_users_normalized_user_name");
    }
}
