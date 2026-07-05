namespace Adeni.Application.Admin;

using Adeni.Domain.Common;

public sealed record AdminCustomerSummary(
    Guid Id,
    string Auth0Sub,
    string Name,
    string? Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ErasureRequestedAt);

public sealed record CustomerDataExportResponse(
    Guid CustomerId,
    string Auth0Sub,
    string Name,
    string? Email,
    string? Phone,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ErasureRequestedAt,
    IReadOnlyList<CustomerBookingExportItem> Bookings);

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
    Task<IReadOnlyList<AdminCustomerSummary>> SearchAsync(
        string? email,
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
