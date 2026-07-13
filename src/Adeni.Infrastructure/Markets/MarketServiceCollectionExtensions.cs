namespace Adeni.Infrastructure.Markets;

using Adeni.Application.Admin;
using Adeni.Application.Markets;
using Adeni.Infrastructure.Admin;
using Microsoft.Extensions.DependencyInjection;

public static class MarketServiceCollectionExtensions
{
    public static IServiceCollection AddAdeniMarkets(this IServiceCollection services)
    {
        services.AddSingleton<MarketCatalogState>();
        services.AddSingleton<IMarketCatalog, SyncMarketCatalog>();
        services.AddScoped<IMarketCatalogLoader, MarketCatalogLoader>();
        services.AddScoped<IAdminMarketService, AdminMarketService>();
        return services;
    }
}
