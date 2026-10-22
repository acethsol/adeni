namespace Adeni.Infrastructure.Admin;

using Adeni.Application.Admin;
using Adeni.Application.Reporting;
using Adeni.Application.Subscriptions;

public sealed class AdminReportingQueryService(
    ISubscriptionMetricsService subscriptionMetrics,
    IAdminRevenueMetricsService revenueMetrics) : IAdminReportingQueryService
{
    public async Task<PlatformSubscriptionMrrResponse> GetPlatformSubscriptionMrrAsync(
        CancellationToken cancellationToken = default)
    {
        var metrics = await subscriptionMetrics.GetAdminMetricsAsync(cancellationToken);
        return new PlatformSubscriptionMrrResponse(
            metrics.SyncedMrrByCurrency
                .Select(x => new PlatformSubscriptionMrrCurrencyTotal(x.Currency, x.AmountMinor))
                .ToArray(),
            metrics.EstimatedMrrByCurrency
                .Select(x => new PlatformSubscriptionMrrCurrencyTotal(x.Currency, x.AmountMinor))
                .ToArray(),
            metrics.MrrSource,
            metrics.PaidTenantCount,
            metrics.PastDueTenantCount,
            metrics.TrialingTenantCount,
            metrics.AsOf);
    }

    public async Task<AdminGmvSummaryResponse> GetGmvAsync(
        string granularity = "month",
        CancellationToken cancellationToken = default)
    {
        var metrics = await revenueMetrics.GetMetricsAsync(granularity, cancellationToken);
        var totalVolume = metrics.Buckets.Sum(x => x.CommerceVolume);
        var totalFees = metrics.Buckets.Sum(x => x.PlatformFeeTotal);

        return new AdminGmvSummaryResponse(
            metrics.Granularity,
            metrics.Buckets
                .Select(x => new AdminGmvPeriodBucket(x.Period, x.Currency, x.CommerceVolume, x.PlatformFeeTotal))
                .ToArray(),
            totalVolume,
            totalFees,
            metrics.AsOf);
    }
}
