namespace Adeni.Application.Discovery;

using Adeni.Domain.Common;

public sealed record DiscoveryBusinessItem(
    Guid TenantId,
    string Name,
    string Slug,
    string CategorySlug,
    string Area,
    double DistanceKm,
    double Latitude,
    double Longitude);

public sealed record DiscoveryResult(
    IReadOnlyList<DiscoveryBusinessItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PublicBusinessProfile(
    Guid TenantId,
    string Name,
    string Slug,
    string CategorySlug,
    string Area,
    string AddressLine,
    string Description,
    string PhoneMasked,
    double? Latitude,
    double? Longitude);

public interface IDiscoveryService
{
    Task<Result<DiscoveryResult>> SearchAsync(
        double latitude,
        double longitude,
        string? categorySlug,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<PublicBusinessProfile>> GetPublicProfileBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default);
}
