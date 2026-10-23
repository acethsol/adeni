namespace Adeni.Application.Tenancy;

using Adeni.Application.Common;
using Adeni.Domain.Common;

public sealed record TenantCustomerUpsertResult(Guid TenantCustomerId, Guid? LinkedPlatformCustomerId);

public sealed record TenantCustomerListItemResponse(
    Guid Id,
    string DisplayName,
    string PhoneMasked,
    string? Email,
    DateTimeOffset LastSeenAt,
    Guid? PlatformCustomerId);

public sealed record TenantCustomerRecentBookingResponse(
    Guid Id,
    string ServiceName,
    DateTimeOffset StartAt,
    int Status,
    string? StaffDisplayName);

public sealed record TenantCustomerRecentQueueVisitResponse(
    Guid Id,
    DateTimeOffset JoinedAt,
    int Status,
    string? ServiceName,
    string? StaffDisplayName);

public sealed record TenantCustomerDetailResponse(
    Guid Id,
    string DisplayName,
    string PhoneMasked,
    string? Email,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? LastBookingAt,
    DateTimeOffset? LastQueueJoinAt,
    Guid? PlatformCustomerId,
    IReadOnlyList<Guid> RecentBookingIds,
    IReadOnlyList<Guid> RecentQueueTicketIds,
    IReadOnlyList<TenantCustomerRecentBookingResponse> RecentBookings,
    IReadOnlyList<TenantCustomerRecentQueueVisitResponse> RecentQueueVisits);

public sealed record TenantCustomerListResponse(
    IReadOnlyList<TenantCustomerListItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public interface ITenantCustomerService
{
    Task<Result<TenantCustomerUpsertResult>> EnsureFromBookingAsync(
        Guid tenantId,
        Guid platformCustomerId,
        CancellationToken cancellationToken = default);

    Task<Result<TenantCustomerUpsertResult>> UpsertFromWalkInAsync(
        Guid tenantId,
        string displayName,
        string phone,
        string? email,
        CancellationToken cancellationToken = default);

    Task<Result<TenantCustomerDetailResponse>> GetByIdAsync(
        Guid tenantId,
        Guid tenantCustomerId,
        Guid? locationId = null,
        CancellationToken cancellationToken = default);

    Task<Result<TenantCustomerListResponse>> ListForTenantAsync(
        Guid tenantId,
        string? search,
        int page,
        int pageSize,
        string? discoverFilter = null,
        string? sortBy = null,
        ListSortDirection? sortDirection = null,
        Guid? locationId = null,
        CancellationToken cancellationToken = default);
}
