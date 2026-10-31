using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dayloom.IntegrationTests;

public class ApiDocsTests
{
    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    public async Task Are_served_in_development(string path)
    {
        using var factory = CreateFactory("Development");

        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Describe_the_auth_endpoints()
    {
        using var factory = CreateFactory("Development");

        var document = await factory.CreateClient().GetStringAsync("/openapi/v1.json");

        Assert.Contains("/api/auth/register", document);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    public async Task Are_hidden_in_production(string path)
    {
        using var factory = CreateFactory("Production");

        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The docs never touch the database, so these tests need no container: the connection string only has to be set.
    private static WebApplicationFactory<Program> CreateFactory(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .UseSetting("ConnectionStrings:Default", "Host=127.0.0.1;Port=1;Database=dayloom;Username=dayloom;Password=unused"));
}
