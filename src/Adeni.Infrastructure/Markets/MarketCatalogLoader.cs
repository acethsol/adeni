namespace Adeni.Infrastructure.Markets;

using System.Text.Json;
using Adeni.Application.Caching;
using Adeni.Application.Markets;
using Adeni.Domain.Catalog;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

public sealed class MarketCatalogLoader(
    AdeniDbContext dbContext,
    ICacheService cache,
    MarketCatalogState state,
    IHostEnvironment environment) : IMarketCatalogLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        await MarketCatalogSeeder.SeedIfEmptyAsync(dbContext, environment, cancellationToken);

        var markets = await cache.GetOrCreateAsync(
            CacheKeys.MarketsAll,
            CacheTtl.Markets,
            LoadFromDatabaseAsync,
            cancellationToken);

        state.Update(markets);
    }

    public async Task InvalidateAndReloadAsync(CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(CacheKeys.MarketsAll, cancellationToken);
        await EnsureLoadedAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<MarketDefinition>> LoadFromDatabaseAsync(
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.CatalogMarkets
            .AsNoTracking()
            .OrderBy(market => market.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(MapToDefinition).ToArray();
    }

    internal static MarketDefinition MapToDefinition(CatalogMarket row)
    {
        var languages = JsonSerializer.Deserialize<List<string>>(row.LanguagesJson, SerializerOptions)
            ?? [];

        return new MarketDefinition(
            row.Id,
            row.Name,
            row.CountryCode,
            row.Currency,
            row.TimeZoneId,
            new MarketLocation(row.DefaultLat, row.DefaultLng),
            languages,
            row.IsLive,
            row.LaunchNote);
    }
}
