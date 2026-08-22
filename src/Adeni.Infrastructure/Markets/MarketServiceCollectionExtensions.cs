namespace Adeni.Infrastructure.Markets;

using Adeni.Application.Markets;
using Microsoft.Extensions.DependencyInjection;

public static class MarketServiceCollectionExtensions
{
    public static IServiceCollection AddAdeniMarkets(this IServiceCollection services)
    {
        services.AddSingleton<MarketCatalogState>();
        services.AddSingleton<IMarketCatalog, SyncMarketCatalog>();
        services.AddScoped<IMarketCatalogLoader, MarketCatalogLoader>();
        return services;
    }
}
