namespace Adeni.Application.Dashboard;

using Adeni.Domain.Common;

public sealed record TenantDashboardSummaryResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    int PendingCount,
    int AppointmentsCount,
    decimal? CollectedAmount,
    decimal? BookedValueAmount,
    string? Currency,
    bool AnalyticsIncluded);

public sealed record TenantDashboardSeriesPoint(DateOnly Date, decimal Value);

public sealed record TenantDashboardSeriesResponse(
    string Metric,
    DateTimeOffset From,
    DateTimeOffset To,
    string? Currency,
    IReadOnlyList<TenantDashboardSeriesPoint> Points);

public sealed record TenantDashboardBreakdownItem(string Key, string Label, decimal Value);

public sealed record TenantDashboardBreakdownResponse(
    string Dimension,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<TenantDashboardBreakdownItem> Items);

public interface ITenantDashboardService
{
    Task<Result<TenantDashboardSummaryResponse>> GetSummaryAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? staffMemberId = null,
        CancellationToken cancellationToken = default);

    Task<Result<TenantDashboardSeriesResponse>> GetSeriesAsync(
        Guid tenantId,
        string metric,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? staffMemberId = null,
        CancellationToken cancellationToken = default);

    Task<Result<TenantDashboardBreakdownResponse>> GetBreakdownAsync(
        Guid tenantId,
        string dimension,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? staffMemberId = null,
        CancellationToken cancellationToken = default);
}
