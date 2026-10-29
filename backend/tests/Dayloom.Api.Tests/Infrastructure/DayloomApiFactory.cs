using Dayloom.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        // Apply the real migrations, so tests also prove the migrations themselves work.
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    /// <summary>A context on the test database, for arranging data and asserting on it directly.</summary>
    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_database.GetConnectionString()).Options);

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
