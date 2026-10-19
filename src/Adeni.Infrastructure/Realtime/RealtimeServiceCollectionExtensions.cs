namespace Adeni.Infrastructure.Realtime;

using Adeni.Application.Realtime;
using Microsoft.Extensions.DependencyInjection;

public static class RealtimeServiceCollectionExtensions
{
    public static IServiceCollection AddRealtimeModule(this IServiceCollection services)
    {
        services.AddSingleton<ITenantRealtimePublisher, NoOpTenantRealtimePublisher>();
        return services;
    }
}
