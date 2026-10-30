using Dayloom.Application.Common.Interfaces;
using Dayloom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dayloom.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseHealthCheckName = "database";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Fail fast: the app is useless without a database, so refuse to start instead of failing on the first request.
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'Default' is missing or blank. In development, set it with: " +
                "dotnet user-secrets set \"ConnectionStrings:Default\" \"<connection string>\"");
        }

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>(DatabaseHealthCheckName);

        return services;
    }
}
