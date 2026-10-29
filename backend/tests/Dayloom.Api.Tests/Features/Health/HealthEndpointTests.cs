using System.Net;
using System.Net.Http.Json;
using Dayloom.Api.Features.Health;
using Dayloom.Api.Tests.Infrastructure;

namespace Dayloom.Api.Tests.Features.Health;

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
}
