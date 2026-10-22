namespace Adeni.Application.Reporting;

public sealed record PlatformSubscriptionMrrResponse(
    IReadOnlyList<PlatformSubscriptionMrrCurrencyTotal> SyncedMrrByCurrency,
    IReadOnlyList<PlatformSubscriptionMrrCurrencyTotal> EstimatedMrrByCurrency,
    string MrrSource,
    int PaidTenantCount,
    int PastDueTenantCount,
    int TrialingTenantCount,
    DateTimeOffset AsOf);

public sealed record PlatformSubscriptionMrrCurrencyTotal(
    string Currency,
    long AmountMinor);

public sealed record AdminGmvSummaryResponse(
    string Granularity,
    IReadOnlyList<AdminGmvPeriodBucket> Buckets,
    decimal TotalCommerceVolume,
    decimal TotalPlatformFees,
    DateTimeOffset AsOf);

public sealed record AdminGmvPeriodBucket(
    string Period,
    string Currency,
    decimal CommerceVolume,
    decimal PlatformFeeTotal);

public interface IAdminReportingQueryService
{
    Task<PlatformSubscriptionMrrResponse> GetPlatformSubscriptionMrrAsync(
        CancellationToken cancellationToken = default);

    Task<AdminGmvSummaryResponse> GetGmvAsync(
        string granularity = "month",
        CancellationToken cancellationToken = default);
}
