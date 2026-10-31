namespace Dayloom.Application.Common.Results;

public enum ErrorType
{
    Validation,
    Conflict,
}

/// <summary>
/// An expected failure of a use case (bad input, a taken email...). Expected failures are returned, not thrown:
/// exceptions are kept for bugs and outages. <see cref="Code"/> is stable and machine-readable so the frontend can
/// show its own Vietnamese message; <see cref="Description"/> is English, for developers.
/// </summary>
public sealed record Error(
    string Code,
    string Description,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static Error Validation(string code, string description, IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new(code, description, ErrorType.Validation, fieldErrors);

    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);
}
