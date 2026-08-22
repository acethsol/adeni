namespace Adeni.Infrastructure.Discovery;

using Adeni.Application.Discovery;
using Microsoft.Extensions.DependencyInjection;

public static class DiscoveryServiceCollectionExtensions
{
    public static IServiceCollection AddDiscoveryModule(this IServiceCollection services)
    {
        services.AddScoped<IDiscoveryService, DiscoveryService>();
        return services;
    }
}
