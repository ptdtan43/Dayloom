using Dayloom.Api.Common;
using Dayloom.Application.Features.Auth.Register;

namespace Dayloom.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth");

        auth.MapPost("/register", RegisterAsync).WithName("Register");

        return app;
    }

    // The endpoint only translates HTTP <-> use case. All rules live in RegisterUserHandler.
    private static async Task<IResult> RegisterAsync(RegisterRequest request, RegisterUserHandler handler)
    {
        var result = await handler.HandleAsync(new RegisterUserCommand(request.Email, request.Password, request.TimeZoneId));

        return result.IsSuccess
            ? Results.Created((string?)null, new RegisterResponse(result.Value.Id, result.Value.Email))
            : result.Error!.ToProblem();
    }
}

/// <param name="TimeZoneId">IANA id from the browser (Intl API); invalid or missing falls back to Asia/Ho_Chi_Minh.</param>
public sealed record RegisterRequest(string? Email, string? Password, string? TimeZoneId);

public sealed record RegisterResponse(Guid Id, string Email);
