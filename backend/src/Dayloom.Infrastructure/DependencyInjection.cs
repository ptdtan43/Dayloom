using Dayloom.Application.Common.Interfaces;
using Dayloom.Application.Features.Auth.Register;
using Dayloom.Infrastructure.Identity;
using Dayloom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        // TryAdd: tests can register a fake clock first.
        services.TryAddSingleton(TimeProvider.System);

        AddIdentity(services);

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>(DatabaseHealthCheckName);

        return services;
    }

    private static void AddIdentity(IServiceCollection services)
    {
        // AddIdentityCore: user management and password hashing only. No cookie authentication or Razor UI,
        // and no MapIdentityApi: Dayloom issues its own JWTs (docs/DESIGN.md §2).
        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                // The user name is the email, already validated by the handler; don't restrict its characters.
                options.User.AllowedUserNameCharacters = string.Empty;

                options.Password.RequiredLength = RegisterUserHandler.MinPasswordLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IIdentityService, IdentityService>();
    }
}
