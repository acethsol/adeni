namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Retention;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class RetentionEntitlementGuard(
    AdeniDbContext db,
    IEntitlementsService entitlements) : IRetentionEntitlementGuard
{
    public async Task<bool> IsLoyaltyBasicEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        return tenant is not null && entitlements.GetEntitlementsForTenant(tenant).LoyaltyBasic;
    }

    public async Task<Result> EnsureLoyaltyBasicEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await IsLoyaltyBasicEntitledAsync(tenantId, cancellationToken)
            ? Result.Success()
            : Result.Failure(RetentionErrorCodes.LoyaltyBasicNotEntitledError());

    public async Task<bool> IsMembershipsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        return tenant is not null && entitlements.GetEntitlementsForTenant(tenant).Memberships;
    }

    public async Task<Result> EnsureMembershipsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await IsMembershipsEntitledAsync(tenantId, cancellationToken)
            ? Result.Success()
            : Result.Failure(RetentionErrorCodes.MembershipsNotEntitledError());

    public async Task<bool> IsRecurringBillingEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return false;
        }

        var entitled = entitlements.GetEntitlementsForTenant(tenant);
        return entitled.Memberships && entitled.RecurringMembershipBilling;
    }

    public async Task<Result> EnsureRecurringBillingEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await IsRecurringBillingEntitledAsync(tenantId, cancellationToken)
            ? Result.Success()
            : Result.Failure(RetentionErrorCodes.RecurringBillingNotEntitledError());

    public async Task<bool> IsPackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        return tenant is not null && entitlements.GetEntitlementsForTenant(tenant).Packages;
    }

    public async Task<Result> EnsurePackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await IsPackagesEntitledAsync(tenantId, cancellationToken)
            ? Result.Success()
            : Result.Failure(RetentionErrorCodes.PackagesNotEntitledError());

    public async Task<bool> IsLoyaltyTiersEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return false;
        }

        var entitled = entitlements.GetEntitlementsForTenant(tenant);
        return entitled.LoyaltyBasic && entitled.LoyaltyTiers;
    }

    public async Task<Result> EnsureLoyaltyTiersEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await IsLoyaltyTiersEntitledAsync(tenantId, cancellationToken)
            ? Result.Success()
            : Result.Failure(RetentionErrorCodes.LoyaltyTiersNotEntitledError());

    public async Task<bool> IsReferralsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return false;
        }

        var entitled = entitlements.GetEntitlementsForTenant(tenant);
        return entitled.LoyaltyBasic && entitled.Referrals;
    }

    public async Task<Result> EnsureReferralsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await IsReferralsEntitledAsync(tenantId, cancellationToken)
            ? Result.Success()
            : Result.Failure(RetentionErrorCodes.ReferralsNotEntitledError());

    public async Task<bool> IsCrossLocationRetentionEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        return tenant is not null && entitlements.GetEntitlementsForTenant(tenant).CrossLocationRetention;
    }

    public async Task<Result> EnsureCrossLocationRetentionEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await IsCrossLocationRetentionEntitledAsync(tenantId, cancellationToken)
            ? Result.Success()
            : Result.Failure(RetentionErrorCodes.CrossLocationRetentionNotEntitledError());

    public async Task<bool> IsAdvancedRetentionAnalyticsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        return tenant is not null && entitlements.GetEntitlementsForTenant(tenant).AdvancedRetentionAnalytics;
    }

    public async Task<Result> EnsureAdvancedRetentionAnalyticsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await IsAdvancedRetentionAnalyticsEntitledAsync(tenantId, cancellationToken)
            ? Result.Success()
            : Result.Failure(RetentionErrorCodes.AdvancedRetentionAnalyticsNotEntitledError());
}