namespace Adeni.Infrastructure.Markets;

using System.Text.Json;
using Adeni.Application.Markets;
using Adeni.Domain.Catalog;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

public static class MarketCatalogSeeder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async Task SeedIfEmptyAsync(
        AdeniDbContext dbContext,
        IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.CatalogMarkets.AnyAsync(cancellationToken))
        {
            return;
        }

        var definitions = MarketCatalogJson.ReadFromFile(environment);
        var now = DateTimeOffset.UtcNow;

        foreach (var market in definitions)
        {
            dbContext.CatalogMarkets.Add(new CatalogMarket
            {
                Id = market.Id,
                Name = market.Name,
                CountryCode = market.CountryCode,
                Currency = market.Currency,
                TimeZoneId = market.TimeZoneId,
                DefaultLat = market.DefaultLocation.Lat,
                DefaultLng = market.DefaultLocation.Lng,
                LanguagesJson = JsonSerializer.Serialize(market.Languages, SerializerOptions),
                IsLive = market.IsLive,
                LaunchNote = market.LaunchNote,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Drops catalog rows that are no longer in <c>markets.json</c>.
    /// V1 go-to-market cities are Lagos and Ottawa; retired ids (Abuja and others) must not stay selectable.
    /// </summary>
    public static async Task<bool> RemoveMarketsNotInFileAsync(
        AdeniDbContext dbContext,
        IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        var allowed = MarketCatalogJson.ReadFromFile(environment)
            .Select(market => market.Id)
            .ToArray();

        var stale = await dbContext.CatalogMarkets
            .Where(market => !allowed.Contains(market.Id))
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return false;
        }

        dbContext.CatalogMarkets.RemoveRange(stale);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
