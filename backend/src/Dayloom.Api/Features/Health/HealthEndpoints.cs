using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using InfrastructureSetup = Dayloom.Infrastructure.DependencyInjection;

namespace Dayloom.Api.Features.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Built-in health checks: Infrastructure registers the database check, the API only decides the response shape.
        // Status code comes from the middleware: 200 when healthy, 503 when the database check fails.
        app.MapHealthChecks("/api/health", new HealthCheckOptions { ResponseWriter = WriteResponseAsync });

        return app;
    }

    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        var databaseUp = report.Entries.TryGetValue(InfrastructureSetup.DatabaseHealthCheckName, out var database)
            && database.Status == HealthStatus.Healthy;

        var response = report.Status == HealthStatus.Healthy
            ? new HealthResponse("ok", "ok")
            : new HealthResponse("degraded", databaseUp ? "ok" : "unreachable");

        return context.Response.WriteAsJsonAsync(response);
    }
}

public record HealthResponse(string Status, string Database);
