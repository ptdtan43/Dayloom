using System.Net;
using System.Net.Http.Json;
using Dayloom.Api.Features.Health;
using Dayloom.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dayloom.IntegrationTests.Features.Health;

public class HealthEndpointTests(DayloomApiFactory factory) : IClassFixture<DayloomApiFactory>
{
    [Fact]
    public async Task Returns_ok_when_database_is_reachable()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal(new HealthResponse("ok", "ok"), body);
    }

    [Fact]
    public async Task Returns_503_when_database_is_unreachable()
    {
        // Nothing listens on port 1, so the database check fails while the API itself is up.
        using var unreachable = new DayloomApiFactoryWithoutDatabase();
        var client = unreachable.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal(new HealthResponse("degraded", "unreachable"), body);
    }

    private sealed class DayloomApiFactoryWithoutDatabase : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Default", "Host=127.0.0.1;Port=1;Database=dayloom;Username=dayloom;Password=unused;Timeout=1");
        }
    }
}
