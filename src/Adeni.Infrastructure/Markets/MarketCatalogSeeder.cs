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
}
