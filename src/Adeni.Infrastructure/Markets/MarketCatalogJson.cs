namespace Adeni.Infrastructure.Markets;

using System.Text.Json;
using System.Text.Json.Serialization;
using Adeni.Application.Markets;
using Microsoft.Extensions.Hosting;

internal static class MarketCatalogJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    internal static IReadOnlyList<MarketDefinition> ReadFromFile(IHostEnvironment environment)
    {
        var catalogPath = ResolveCatalogPath(environment);
        if (!File.Exists(catalogPath))
        {
            throw new FileNotFoundException(
                $"Market catalog not found at '{catalogPath}'. Ensure packages/shared/src/data/markets.json is copied to output.");
        }

        var json = File.ReadAllText(catalogPath);
        var document = JsonSerializer.Deserialize<MarketCatalogDocument>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Market catalog JSON is empty or invalid.");

        return document.Markets
            .Select(market => new MarketDefinition(
                market.Id,
                market.Name,
                market.CountryCode,
                market.Currency,
                market.TimeZoneId,
                new MarketLocation(market.DefaultLocation.Lat, market.DefaultLocation.Lng),
                market.Languages,
                market.IsLive,
                market.LaunchNote))
            .ToArray();
    }

    internal static string ResolveCatalogPath(IHostEnvironment environment)
    {
        var candidates = new[]
        {
            Path.Combine(environment.ContentRootPath, "Markets", "markets.json"),
            Path.Combine(AppContext.BaseDirectory, "Markets", "markets.json"),
            Path.Combine(AppContext.BaseDirectory, "markets.json"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    private sealed record MarketCatalogDocument(IReadOnlyList<MarketCatalogEntry> Markets);

    private sealed record MarketCatalogEntry
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string CountryCode { get; init; }
        public required string Currency { get; init; }
        public required string TimeZoneId { get; init; }
        public required MarketCatalogLocation DefaultLocation { get; init; }
        public required IReadOnlyList<string> Languages { get; init; }
        public required bool IsLive { get; init; }
        public string? LaunchNote { get; init; }
    }

    private sealed record MarketCatalogLocation(
        [property: JsonPropertyName("lat")] double Lat,
        [property: JsonPropertyName("lng")] double Lng);
}
