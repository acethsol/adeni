namespace Adeni.Application.Admin;

public sealed record AdminRevenuePeriodBucket(
    string Period,
    string Currency,
    long SaasAmountMinor,
    decimal CommerceVolume,
    decimal PlatformFeeTotal);

public sealed record AdminCommercePaymentSummary(
    Guid Id,
    Guid TenantId,
    string TenantName,
    decimal Amount,
    decimal PlatformFeeAmount,
    string Currency,
    string Status,
    DateTimeOffset OccurredAt);

public sealed record AdminSaasChargeSummary(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string ProviderReference,
    string EventType,
    string? SubscriptionSku,
    long? AmountMinor,
    string? Currency,
    string Status,
    DateTimeOffset OccurredAt);

public sealed record AdminRevenueMetricsResponse(
    string Granularity,
    IReadOnlyList<AdminRevenuePeriodBucket> Buckets,
    IReadOnlyList<AdminSaasChargeSummary> RecentSaasCharges,
    IReadOnlyList<AdminCommercePaymentSummary> RecentCommercePayments,
    DateTimeOffset AsOf);

public interface IAdminRevenueMetricsService
{
    Task<AdminRevenueMetricsResponse> GetMetricsAsync(
        string granularity,
        CancellationToken cancellationToken = default);
}
