namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;

public sealed record SubscriptionUsageResponse(
    string SubscriptionSku,
    TenantEntitlements Entitlements,
    int BookingsUsedThisMonth,
    int? BookingsLimitThisMonth,
    string? ContractSubscriptionSku = null,
    string BillingStatus = "trialing",
    DateTimeOffset? TrialEndsAt = null,
    DateTimeOffset? PaidThrough = null);

public interface IEntitlementsService
{
    TenantEntitlements GetEntitlementsForTenant(Tenant tenant);

    Task<int> GetBookingsUsedThisMonthAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result> EnsureCanCreateBookingForTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default);

    Task<Result> EnsureCanAddLocationForTenantAsync(
        Tenant tenant,
        int activeLocationCount,
        CancellationToken cancellationToken = default);

    Task<Result> EnsureCanUseMessagingForTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default);

    Task<SubscriptionUsageResponse> GetUsageForTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default);
}
