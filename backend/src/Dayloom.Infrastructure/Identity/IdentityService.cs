using Dayloom.Application.Common.Interfaces;
using Dayloom.Application.Common.Results;
using Dayloom.Application.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dayloom.Infrastructure.Identity;

internal sealed class IdentityService(UserManager<AppUser> users) : IIdentityService
{
    private static readonly HashSet<string> EmailTakenCodes =
        [nameof(IdentityErrorDescriber.DuplicateEmail), nameof(IdentityErrorDescriber.DuplicateUserName)];

    private static readonly HashSet<string> EmailUniqueIndexes = ["ix_users_normalized_email", "ix_users_normalized_user_name"];

    public async Task<Result<Guid>> CreateUserAsync(NewUser newUser, string password)
    {
        var user = new AppUser
        {
            Id = Guid.CreateVersion7(newUser.CreatedAt),
            UserName = newUser.Email, // Dayloom has no separate user name: signing in is by email
            Email = newUser.Email,
            TimeZoneId = newUser.TimeZoneId,
            CreatedAt = newUser.CreatedAt,
        };

        IdentityResult result;
        try
        {
            result = await users.CreateAsync(user, password);
        }
        catch (DbUpdateException exception) when (IsEmailUniqueViolation(exception))
        {
            // Identity checks "email already used?" with a SELECT before the INSERT. Two sign-ups racing with the
            // same email can both pass that check; the unique index then rejects the second INSERT.
            return AuthErrors.EmailTaken;
        }

        if (result.Succeeded)
        {
            return user.Id;
        }

        if (result.Errors.Any(error => EmailTakenCodes.Contains(error.Code)))
        {
            return AuthErrors.EmailTaken;
        }

        // The handler already validated the input, so this is a rule only Identity knows about.
        var field = result.Errors.Any(error => error.Code.StartsWith("Password", StringComparison.Ordinal)) ? "password" : "email";
        return AuthErrors.InvalidRegistration(new Dictionary<string, string[]>
        {
            [field] = result.Errors.Select(error => error.Description).ToArray(),
        });
    }

    private static bool IsEmailUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && EmailUniqueIndexes.Contains(postgres.ConstraintName ?? string.Empty);
}
