using Microsoft.AspNetCore.Identity;

namespace Dayloom.Infrastructure.Identity;

/// <summary>
/// Identity handles email, password hashing and lockout; we only add what Dayloom needs on top.
/// Lives in Infrastructure, not Domain: it inherits from ASP.NET Identity, and Domain must not depend on a framework.
/// Domain entities refer to users by <c>UserId</c> only.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public const string DefaultTimeZoneId = "Asia/Ho_Chi_Minh";

    /// <summary>IANA time zone used for every date boundary shown to this user (docs/DESIGN.md §3).</summary>
    public string TimeZoneId { get; set; } = DefaultTimeZoneId;

    public DateTimeOffset CreatedAt { get; set; }
}
