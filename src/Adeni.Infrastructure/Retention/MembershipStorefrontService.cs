namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Retention;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Discover storefront for membership plans (Sprint 37.2-D). Public read is anonymous and exposes verified
/// businesses only; subscribe is orchestration through <see cref="IMembershipBillingCoordinator"/> — the
/// membership activates only on the idempotent <c>PaymentCompleted</c> signal.
/// </summary>
public sealed class MembershipStorefrontService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    IMembershipBillingCoordinator billing) : IMembershipStorefrontService
{
    public async Task<Result<PublicMembershipPlanCatalogResponse>> GetPublicPlansAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var tenantId = await ResolveTenantAsync(slug, cancellationToken);
        if (tenantId == Guid.Empty || !await entitlements.IsMembershipsEntitledAsync(tenantId, cancellationToken))
        {
            return Result.Success(new PublicMembershipPlanCatalogResponse([], false));
        }

        var plans = await db.Set<MembershipPlan>().AsNoTracking()
            .Include(x => x.Benefits)
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Price).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var items = plans
            .Select(x => new PublicMembershipPlanResponse(
                x.Id,
                x.Name,
                x.Description,
                x.Price,
                x.Currency,
                MembershipPlanCodes.ToApi(x.BillingInterval),
                MembershipPlanCodes.ToApi(x.RolloverPolicy),
                MembershipPlanCodes.ToApi(x.CancellationPolicy),
                x.AllowFreeze,
                x.PastDueGraceDays,
                x.Benefits.OrderBy(b => b.SortOrder)
                    .Select(b => new MembershipBenefitResponse(b.Id, b.ServiceOfferingId, b.ServiceName, b.QuantityPerCycle))
                    .ToArray()))
            .ToArray();

        var subscribeEnabled = items.Length > 0
            && billing.OnlineBillingEnabled
            && await entitlements.IsRecurringBillingEntitledAsync(tenantId, cancellationToken);
        return Result.Success(new PublicMembershipPlanCatalogResponse(items, subscribeEnabled));
    }

    public async Task<Result<MembershipSubscribeResponse>> SubscribeAsync(
        string customerAuth0Sub,
        string slug,
        Guid membershipPlanId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await ResolveTenantAsync(slug, cancellationToken);
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<MembershipSubscribeResponse>(RetentionErrorCodes.MembershipPlanNotFoundError());
        }

        return await billing.StartOnlineSubscriptionAsync(
            tenantId, customerAuth0Sub, membershipPlanId, idempotencyKey, cancellationToken);
    }

    private Task<Guid> ResolveTenantAsync(string slug, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(slug)
            ? Task.FromResult(Guid.Empty)
            : VerifiedLocationQueries.ResolveTenantIdBySlugAsync(db, slug.Trim().ToLowerInvariant(), cancellationToken);
}

/// <summary>
/// <c>TenantMembershipMrr</c> (Retention-owned; not Adeni SaaS MRR). Counts live paying memberships
/// (<c>Active</c> + <c>PastDue</c>) at their snapshotted price normalized to one month. Frozen, cancel-pending,
/// pending and terminal memberships are excluded. Currencies are never mixed.
/// </summary>
public sealed class MembershipMrrService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    TimeProvider clock) : IMembershipMrrService
{
    public async Task<Result<TenantMembershipMrrResponse>> GetAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureMembershipsEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<TenantMembershipMrrResponse>(entitled.Error);
        }

        var counted = MembershipMrrMath.CountedStatuses;
        var groups = await db.Set<CustomerMembership>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && counted.Contains(x.Status))
            .GroupBy(x => new { x.CurrencySnapshot, x.BillingIntervalSnapshot })
            .Select(g => new { g.Key.CurrencySnapshot, g.Key.BillingIntervalSnapshot, Total = g.Sum(x => x.PriceSnapshot), Count = g.Count() })
            .ToListAsync(cancellationToken);

        var items = groups
            .GroupBy(x => x.CurrencySnapshot)
            .Select(currency => new TenantMembershipMrr(
                currency.Key,
                currency.Sum(x => MembershipMrrMath.ToMonthly(x.Total, x.BillingIntervalSnapshot)),
                currency.Sum(x => x.Count)))
            .OrderBy(x => x.Currency)
            .ToArray();
        return Result.Success(new TenantMembershipMrrResponse(items, clock.GetUtcNow()));
    }
}
