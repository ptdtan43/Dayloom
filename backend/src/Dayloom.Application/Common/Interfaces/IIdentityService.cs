using Dayloom.Application.Common.Results;

namespace Dayloom.Application.Common.Interfaces;

/// <summary>
/// User accounts as the Application layer needs them. Infrastructure implements this with ASP.NET Identity,
/// so use cases never touch UserManager, password hashing or the users table directly.
/// </summary>
public interface IIdentityService
{
    /// <summary>Creates the account. Fails with <c>auth.email_taken</c> if the email is already registered.</summary>
    Task<Result<Guid>> CreateUserAsync(NewUser user, string password);
}

public sealed record NewUser(string Email, string TimeZoneId, DateTimeOffset CreatedAt);
