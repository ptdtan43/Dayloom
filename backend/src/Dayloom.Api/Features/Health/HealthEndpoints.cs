using Dayloom.Api.Data;

namespace Dayloom.Api.Features.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var databaseUp = await db.Database.CanConnectAsync(cancellationToken);

            return databaseUp
                ? Results.Ok(new HealthResponse("ok", "ok"))
                : Results.Json(new HealthResponse("degraded", "unreachable"), statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithName("GetHealth");

        return app;
    }
}

public record HealthResponse(string Status, string Database);
