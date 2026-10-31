using System.Diagnostics;
using Dayloom.Application.Common.Results;

namespace Dayloom.Api.Common;

public static class ErrorResults
{
    /// <summary>
    /// Turns a use-case error into an RFC 9457 problem response. The stable error code goes in a "code" field,
    /// which the frontend maps to a Vietnamese message.
    /// </summary>
    public static IResult ToProblem(this Error error)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        return error.Type switch
        {
            ErrorType.Validation => Results.ValidationProblem(
                new Dictionary<string, string[]>(error.FieldErrors ?? new Dictionary<string, string[]>()),
                detail: error.Description,
                extensions: extensions),
            ErrorType.Conflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: error.Description,
                extensions: extensions),
            _ => throw new UnreachableException($"No HTTP mapping for error type {error.Type}."),
        };
    }
}
