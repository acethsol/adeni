namespace Adeni.Infrastructure.Discovery;

internal sealed class DiscoverySearchRow
{
    public Guid LocationId { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string LocationName { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string CategorySlug { get; set; } = string.Empty;

    public string Area { get; set; } = string.Empty;

    public string MarketId { get; set; } = string.Empty;

    public string? CoverImageKey { get; set; }

    public double DistanceKm { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double? RatingAvg { get; set; }

    public int ReviewCount { get; set; }
}
