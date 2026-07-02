namespace Adeni.Application.Booking;

using Adeni.Domain.Booking;
using Adeni.Domain.Common;

public sealed record ServiceOfferingResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal PriceAmount,
    string Currency,
    int DurationMinutes,
    bool IsActive);

public sealed record CreateServiceOfferingRequest(
    string Name,
    string? Description,
    decimal PriceAmount,
    string Currency,
    int DurationMinutes);

public sealed record UpdateServiceOfferingRequest(
    string Name,
    string? Description,
    decimal PriceAmount,
    string Currency,
    int DurationMinutes,
    bool IsActive);

public interface IServiceCatalogService
{
    Task<IReadOnlyList<ServiceOfferingResponse>> ListForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceOfferingResponse>> ListPublicBySlugAsync(
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
