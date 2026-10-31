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

public sealed record StaffRosterExportRow(
    string DisplayName,
    string RoleKey,
    bool IsActive,
    string PortalAccessStatus,
    int ServiceCount,
    int SortOrder);

public sealed record TenantCustomerExportRow(
    Guid Id,
    string DisplayName,
    string PhoneMasked,
    string? Email,
    string ProfileType,
    DateTimeOffset LastSeenAt,
    DateTimeOffset CreatedAt);

public sealed record TenantLocationExportRow(
    string Slug,
    string Name,
    string MarketId,
    string AddressLine,
    string Area,
    bool IsPrimary,
    bool IsActive);

public sealed record TenantServiceExportRow(
    string Name,
    string? Description,
    decimal PriceAmount,
    string Currency,
    int DurationMinutes,
    string? CategorySlug,
    bool IsActive,
    bool IsAddOn,
    int SortOrder);

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
        Guid? locationId = null,
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

    Task<Result<IReadOnlyList<StaffRosterExportRow>>> GetStaffRosterAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TenantCustomerExportRow>>> GetTenantCustomersAsync(
        Guid tenantId,
        Guid? locationId = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TenantLocationExportRow>>> GetTenantLocationsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TenantServiceExportRow>>> GetTenantServicesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AttendanceRegisterExportRow>>> GetAttendanceRegisterAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? locationId,
        CancellationToken cancellationToken = default);
}

public sealed record AttendanceRegisterExportRow(
    DateTimeOffset OccurredAt,
    string StaffDisplayName,
    string EventType,
    string LocationName,
    string LocationVerificationStatus,
    string VerificationMethod,
    int? DistanceFromLocationMeters);
