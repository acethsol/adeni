namespace Adeni.Domain.Catalog;

public sealed class CatalogMarket
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = string.Empty;

    public double DefaultLat { get; set; }

    public double DefaultLng { get; set; }

    public string LanguagesJson { get; set; } = "[]";

    public bool IsLive { get; set; }

    public string? LaunchNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
