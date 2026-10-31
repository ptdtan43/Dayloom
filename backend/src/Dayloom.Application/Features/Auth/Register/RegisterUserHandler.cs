using System.Net.Mail;
using Dayloom.Application.Common;
using Dayloom.Application.Common.Interfaces;
using Dayloom.Application.Common.Results;

namespace Dayloom.Application.Features.Auth.Register;

public sealed record RegisterUserCommand(string? Email, string? Password, string? TimeZoneId);

public sealed record RegisteredUser(Guid Id, string Email);

/// <summary>Use case: create an account with email + password (docs/DESIGN.md §1, Auth).</summary>
public sealed class RegisterUserHandler(IIdentityService identity, TimeProvider clock)
{
    public const int MaxEmailLength = 256;

    // NIST guidance: length matters more than character classes, so no "must contain a digit" rules.
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;

    public async Task<Result<RegisteredUser>> HandleAsync(RegisterUserCommand command)
    {
        var email = command.Email?.Trim() ?? string.Empty;
        var password = command.Password ?? string.Empty;

        var errors = Validate(email, password);
        if (errors.Count > 0)
        {
            return AuthErrors.InvalidRegistration(errors);
        }

        var newUser = new NewUser(email, TimeZones.ResolveOrDefault(command.TimeZoneId), clock.GetUtcNow());
        var created = await identity.CreateUserAsync(newUser, password);

        return created.IsSuccess ? new RegisteredUser(created.Value, email) : created.Error!;
    }

    private static Dictionary<string, string[]> Validate(string email, string password)
    {
        var errors = new Dictionary<string, string[]>();

        if (email.Length == 0)
        {
            errors["email"] = ["Email is required."];
        }
        else if (email.Length > MaxEmailLength || !IsPlainEmailAddress(email))
        {
            errors["email"] = ["Email is not a valid address."];
        }

        if (password.Length is < MinPasswordLength or > MaxPasswordLength)
        {
            errors["password"] = [$"Password must be {MinPasswordLength}-{MaxPasswordLength} characters."];
        }

        return errors;
    }

    // MailAddress also accepts display-name forms like "Tan <tan@example.com>"; require the bare address.
    private static bool IsPlainEmailAddress(string email) =>
        MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;
}
