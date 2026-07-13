namespace Adeni.Application.Discovery;

using Adeni.Domain.Common;

public sealed record DiscoveryBusinessItem(
    Guid LocationId,
    Guid TenantId,
    string Name,
    string LocationName,
    string Slug,
    string CategorySlug,
    string Area,
    string MarketId,
    string? CoverImageUrl,
    double? RatingAvg,
    int ReviewCount,
    double DistanceKm,
    double Latitude,
    double Longitude);

public sealed record DiscoveryResult(
    IReadOnlyList<DiscoveryBusinessItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PublicBusinessProfile(
    Guid LocationId,
    Guid TenantId,
    string Name,
    string LocationName,
    string Slug,
    string CategorySlug,
    string Area,
    string MarketId,
    string AddressLine,
    string Description,
    string PhoneMasked,
    string? CoverImageUrl,
    double? RatingAvg,
    int ReviewCount,
    double? Latitude,
    double? Longitude);

public interface IDiscoveryService
{
    Task<Result<DiscoveryResult>> SearchAsync(
        double latitude,
        double longitude,
        string? categorySlug,
        string? marketId,
        string? query,
        int page,
        int pageSize,
        DiscoverySort sort = DiscoverySort.Distance,
        int? minRating = null,
        CancellationToken cancellationToken = default);

    Task<Result<PublicBusinessProfile>> GetPublicProfileBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default);
}
