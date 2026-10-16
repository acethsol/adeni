namespace Adeni.Application.Subscriptions;

public sealed record AdminSubscriptionMetricsCurrencyTotal(
    string Currency,
    long AmountMinor);

public sealed record AdminSubscriptionMetricsResponse(
    int PaidTenantCount,
    int PastDueTenantCount,
    int TrialingTenantCount,
    IReadOnlyList<AdminSubscriptionSkuCount> SkuMix,
    IReadOnlyList<AdminSubscriptionMetricsCurrencyTotal> SyncedMrrByCurrency,
    IReadOnlyList<AdminSubscriptionMetricsCurrencyTotal> EstimatedMrrByCurrency,
    string MrrSource,
    DateTimeOffset AsOf);

public sealed record AdminSubscriptionSkuCount(
    string Sku,
    int Count);

public interface ISubscriptionMetricsService
{
    Task<AdminSubscriptionMetricsResponse> GetAdminMetricsAsync(
        CancellationToken cancellationToken = default);
}
