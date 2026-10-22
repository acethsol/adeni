namespace Adeni.Application.Reporting;

using Adeni.Domain.Common;

public sealed record BookingsRegisterExportRow(
    Guid BookingId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string Status,
    string ServiceName,
    string? CustomerName,
    string? StaffDisplayName,
    decimal? TotalAmount,
    string? Currency);

public sealed record SalesSummaryExportRow(
    string Metric,
    string Value,
    string? Currency);

public sealed record CustomerStatementLine(
    DateTimeOffset OccurredAt,
    string Description,
    decimal? Amount,
    string? Currency);

public sealed record CustomerStatementData(
    string BusinessName,
    string CustomerDisplayName,
    string CustomerPhoneMasked,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<CustomerStatementLine> Lines);

public interface ITenantReportExportQueryService
{
    Task<Result<IReadOnlyList<BookingsRegisterExportRow>>> GetBookingsRegisterAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? staffMemberId,
        Guid? locationId,
        CancellationToken cancellationToken = default);

    Task<Result<SalesSummaryExportRow[]>> GetSalesSummaryAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? staffMemberId,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerStatementData>> GetCustomerStatementAsync(
        Guid tenantId,
        Guid tenantCustomerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<int> GetActiveLocationCountAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
