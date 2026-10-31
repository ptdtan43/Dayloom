using Dayloom.Application.Common.Results;

namespace Dayloom.Application.Features.Auth;

public static class AuthErrors
{
    public static readonly Error EmailTaken =
        Error.Conflict("auth.email_taken", "An account with this email already exists.");

    public static Error InvalidRegistration(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        Error.Validation("auth.invalid_registration", "One or more registration fields are invalid.", fieldErrors);
}
