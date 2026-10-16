namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Common;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;

public sealed record SubscriptionUsageResponse(
    string Tier,
    TenantEntitlements Entitlements,
    int BookingsUsedThisMonth,
    int? BookingsLimitThisMonth,
    string SubscriptionSku = "starter",
    string BillingStatus = "trialing",
    DateTimeOffset? TrialEndsAt = null,
    DateTimeOffset? PaidThrough = null);

public interface IEntitlementsService
{
    TenantEntitlements GetEntitlements(SubscriptionTier tier);

    TenantEntitlements GetEntitlementsForTenant(Tenant tenant);

    Task<int> GetBookingsUsedThisMonthAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result> EnsureCanCreateBookingAsync(
        Guid tenantId,
        SubscriptionTier tier,
        CancellationToken cancellationToken = default);

    Task<Result> EnsureCanCreateBookingForTenantAsync(
        Tenant tenant,
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

    Task<Result> EnsureCanUseMessagingForTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default);

    Task<SubscriptionUsageResponse> GetUsageAsync(
        Guid tenantId,
        SubscriptionTier tier,
        CancellationToken cancellationToken = default);

    Task<SubscriptionUsageResponse> GetUsageForTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default);
}
