using Dayloom.Application.Common;
using Microsoft.AspNetCore.Identity;

namespace Dayloom.Infrastructure.Identity;

/// <summary>
/// Identity handles email, password hashing and lockout; we only add what Dayloom needs on top.
/// Lives in Infrastructure, not Domain: it inherits from ASP.NET Identity, and Domain must not depend on a framework.
/// Domain entities refer to users by <c>UserId</c> only.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    /// <summary>IANA time zone used for every date boundary shown to this user (docs/DESIGN.md §3).</summary>
    public string TimeZoneId { get; set; } = TimeZones.Default;

    public DateTimeOffset CreatedAt { get; set; }
}
