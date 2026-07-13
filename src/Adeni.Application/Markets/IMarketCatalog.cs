namespace Adeni.Application.Markets;

public sealed record MarketLocation(double Lat, double Lng);

public sealed record MarketDefinition(
    string Id,
    string Name,
    string CountryCode,
    string Currency,
    string TimeZoneId,
    MarketLocation DefaultLocation,
    IReadOnlyList<string> Languages,
    bool IsLive,
    string? LaunchNote = null);

public interface IMarketCatalog
{
    IReadOnlyList<MarketDefinition> List();

    IReadOnlyList<MarketDefinition> ListLive();

    MarketDefinition? GetById(string marketId);

    bool IsValid(string? marketId);

    string Normalize(string marketId);
}
