using Dayloom.Application.Features.Auth.Register;
using Microsoft.Extensions.DependencyInjection;

namespace Dayloom.Application;

public static class DependencyInjection
{
    /// <summary>Registers use-case handlers. Each layer registers its own services; Program.cs only calls these.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<RegisterUserHandler>();

        return services;
    }
}
