using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Dayloom.Api.Tests.Infrastructure;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL container, so integration tests hit a real database
/// (same engine and version as production) instead of an in-memory fake.
/// </summary>
public class DayloomApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Keep in sync with the image in docker-compose.yml.
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public async Task InitializeAsync() => await _database.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _database.GetConnectionString());
    }

    public new async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        await base.DisposeAsync();
    }
}
