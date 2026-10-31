namespace Adeni.Application.Admin;

using Adeni.Domain.Common;

public sealed record AdminCustomerSummary(
    Guid Id,
    string Auth0Sub,
    string Name,
    string? Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ErasureRequestedAt,
    DateTimeOffset? EmailVerifiedAt = null,
    DateTimeOffset? PhoneVerifiedAt = null);

public sealed record CustomerDataExportResponse(
    Guid CustomerId,
    string Auth0Sub,
    string Name,
    string? Email,
    string? Phone,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ErasureRequestedAt,
    DateTimeOffset? EmailVerifiedAt,
    DateTimeOffset? PhoneVerifiedAt,
    IReadOnlyList<CustomerBookingExportItem> Bookings,
    IReadOnlyList<Adeni.Application.Tenancy.CustomerSegmentsResponse>? CustomerSegments = null);

public sealed record CustomerBookingExportItem(
    Guid Id,
    Guid TenantId,
    string ServiceName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Status,
    string? CustomerNotes,
    DateTimeOffset CreatedAt);

public interface IAdminCustomerService
{
    Task<Result<AdminPagedResult<AdminCustomerSummary>>> SearchAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerDataExportResponse>> ExportAsync(
        Guid customerId,
        string adminId,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> InitiateErasureAsync(
        Guid customerId,
        string adminId,
        CancellationToken cancellationToken = default);
}
