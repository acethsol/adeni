namespace Adeni.Application.Booking;

using Adeni.Domain.Booking;
using Adeni.Domain.Common;

public sealed record ServiceOfferingResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal PriceAmount,
    string Currency,
    string PricingType,
    int DurationMinutes,
    bool IsActive,
    string? CategorySlug = null,
    string? CatalogServiceId = null,
    string BookingDeliveryType = "appointment",
    Guid? MenuGroupId = null,
    int SortOrder = 0);

public sealed record CreateServiceOfferingRequest(
    string Name,
    string? Description,
    decimal PriceAmount,
    string Currency,
    int DurationMinutes,
    string PricingType = "fixed",
    string? CategorySlug = null,
    string? CatalogServiceId = null,
    string BookingDeliveryType = "appointment",
    Guid? MenuGroupId = null,
    int SortOrder = 0);

public sealed record UpdateServiceOfferingRequest(
    string Name,
    string? Description,
    decimal PriceAmount,
    string Currency,
    int DurationMinutes,
    string PricingType,
    bool IsActive,
    string? CategorySlug = null,
    string? CatalogServiceId = null,
    string BookingDeliveryType = "appointment",
    Guid? MenuGroupId = null,
    int SortOrder = 0);

public sealed record ServiceCatalogListResponse(
    IReadOnlyList<ServiceOfferingResponse> Items,
    IReadOnlyList<ServiceMenuGroupResponse> Groups);

public interface IServiceCatalogService
{
    Task<ServiceCatalogListResponse> ListForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ServiceCatalogListResponse> ListPublicBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default);

    Task<Result<ServiceOfferingResponse>> CreateAsync(
        Guid tenantId,
        CreateServiceOfferingRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ServiceOfferingResponse>> UpdateAsync(
        Guid tenantId,
        Guid serviceId,
        UpdateServiceOfferingRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeactivateAsync(
        Guid tenantId,
        Guid serviceId,
        CancellationToken cancellationToken = default);
}
