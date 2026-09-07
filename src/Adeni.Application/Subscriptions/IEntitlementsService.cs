namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Common;
using Adeni.Domain.Subscriptions;

public sealed record SubscriptionUsageResponse(
    string Tier,
    TenantEntitlements Entitlements,
    int BookingsUsedThisMonth,
    int? BookingsLimitThisMonth);

public interface IEntitlementsService
{
    TenantEntitlements GetEntitlements(SubscriptionTier tier);

    Task<int> GetBookingsUsedThisMonthAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result> EnsureCanCreateBookingAsync(
        Guid tenantId,
        SubscriptionTier tier,
        CancellationToken cancellationToken = default);

    Task<Result> EnsureCanAddLocationAsync(
        Guid tenantId,
        SubscriptionTier tier,
        int activeLocationCount,
        CancellationToken cancellationToken = default);

    Task<Result> EnsureCanUseMessagingAsync(
        Guid tenantId,
        SubscriptionTier tier,
        CancellationToken cancellationToken = default);

    Task<SubscriptionUsageResponse> GetUsageAsync(
        Guid tenantId,
        SubscriptionTier tier,
        CancellationToken cancellationToken = default);
}
