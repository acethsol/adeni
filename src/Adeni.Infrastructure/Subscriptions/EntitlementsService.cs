namespace Adeni.Infrastructure.Subscriptions;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class EntitlementsService(AdeniDbContext dbContext) : IEntitlementsService
{
    public TenantEntitlements GetEntitlementsForTenant(Tenant tenant) =>
        SubscriptionEntitlements.ForSku(TenantBillingPolicy.ResolveEffectiveSku(tenant));

    public async Task<int> GetBookingsUsedThisMonthAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var monthStart = new DateTimeOffset(
            DateTimeOffset.UtcNow.Year,
            DateTimeOffset.UtcNow.Month,
            1,
            0,
            0,
            0,
            TimeSpan.Zero);

        return await dbContext.Bookings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.CreatedAt >= monthStart
                && x.Status != BookingStatus.Cancelled)
            .CountAsync(cancellationToken);
    }

    public Task<Result> EnsureCanCreateBookingForTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default)
    {
        var entitlements = GetEntitlementsForTenant(tenant);
        return EnsureBookingLimitAsync(tenant.Id, entitlements, cancellationToken);
    }

    public Task<Result> EnsureCanAddLocationForTenantAsync(
        Tenant tenant,
        int activeLocationCount,
        CancellationToken cancellationToken = default)
    {
        var entitlements = GetEntitlementsForTenant(tenant);
        if (entitlements.MultiLocation || activeLocationCount == 0)
        {
            return Task.FromResult(Result.Success());
        }

        return Task.FromResult(Result.Failure(ErrorCodes.MultiLocationRequiredError()));
    }

    public Task<Result> EnsureCanUseMessagingForTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default)
    {
        var entitlements = GetEntitlementsForTenant(tenant);
        return Task.FromResult(EnsureMessagingEntitled(entitlements));
    }

    public async Task<SubscriptionUsageResponse> GetUsageForTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default)
    {
        var effectiveSku = TenantBillingPolicy.ResolveEffectiveSku(tenant);
        var entitlements = SubscriptionEntitlements.ForSku(effectiveSku);
        var used = await GetBookingsUsedThisMonthAsync(tenant.Id, cancellationToken);
        var contractSku = tenant.SubscriptionSku;
        var contractApi = SubscriptionSkuMapping.ToApiValue(contractSku);

        return new SubscriptionUsageResponse(
            SubscriptionSkuMapping.ToApiValue(effectiveSku),
            entitlements,
            used,
            entitlements.MonthlyBookingLimit,
            contractApi != SubscriptionSkuMapping.ToApiValue(effectiveSku) ? contractApi : null,
            TenantBillingStatusMapping.ToApiValue(tenant.BillingStatus),
            tenant.TrialEndsAt,
            tenant.PaidThrough);
    }

    private static Result EnsureMessagingEntitled(TenantEntitlements entitlements) =>
        entitlements.Messaging
            ? Result.Success()
            : Result.Failure(ErrorCodes.MessagingNotEntitledError());

    private async Task<Result> EnsureBookingLimitAsync(
        Guid tenantId,
        TenantEntitlements entitlements,
        CancellationToken cancellationToken)
    {
        if (entitlements.MonthlyBookingLimit is null)
        {
            return Result.Success();
        }

        var used = await GetBookingsUsedThisMonthAsync(tenantId, cancellationToken);
        if (used >= entitlements.MonthlyBookingLimit.Value)
        {
            return Result.Failure(ErrorCodes.BookingLimitReachedError(entitlements.MonthlyBookingLimit.Value));
        }

        return Result.Success();
    }
}
