namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Retention;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class RetentionAnalyticsService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    IMembershipMrrService membershipMrr,
    TimeProvider clock) : IRetentionAnalyticsService
{
    public async Task<Result<TenantMembershipMrrResponse>> GetMembershipMrrAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureAdvancedRetentionAnalyticsEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<TenantMembershipMrrResponse>(entitled.Error);
        }

        return await membershipMrr.GetAsync(tenantId, cancellationToken);
    }

    public async Task<Result<RetentionAnalyticsSummaryResponse>> GetSummaryAsync(
        Guid tenantId,
        RetentionAnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureAdvancedRetentionAnalyticsEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<RetentionAnalyticsSummaryResponse>(entitled.Error);
        }

        if (query.To < query.From)
        {
            return Result.Failure<RetentionAnalyticsSummaryResponse>(
                RetentionErrorCodes.ValidationError("'to' must be on or after 'from'."));
        }

        var packages = await PackageMetricsAsync(tenantId, query, cancellationToken);
        TenantMembershipMrrResponse? mrr = null;
        var mrrResult = await membershipMrr.GetAsync(tenantId, cancellationToken);
        if (mrrResult.IsSuccess)
        {
            mrr = mrrResult.Value;
        }

        LoyaltyPointsMetrics? loyalty = null;
        if (await entitlements.IsLoyaltyBasicEntitledAsync(tenantId, cancellationToken))
        {
            loyalty = await LoyaltyMetricsAsync(tenantId, query, cancellationToken);
        }

        return Result.Success(new RetentionAnalyticsSummaryResponse(
            mrr,
            packages,
            loyalty,
            clock.GetUtcNow()));
    }

    private async Task<PackageUtilizationMetrics> PackageMetricsAsync(
        Guid tenantId,
        RetentionAnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        var ledger = db.Set<BenefitTransaction>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.OccurredAt >= query.From && x.OccurredAt <= query.To);
        if (query.BusinessLocationId is Guid locationId)
        {
            ledger = ledger.Where(x => x.BusinessLocationId == locationId);
        }

        var rows = await ledger
            .GroupBy(x => x.Kind)
            .Select(g => new { Kind = g.Key, Total = g.Sum(x => x.Quantity) })
            .ToListAsync(cancellationToken);

        static int Sum(IEnumerable<(BenefitTransactionKind Kind, int Total)> source, BenefitTransactionKind kind) =>
            source.Where(x => x.Kind == kind).Sum(x => x.Total);

        var mapped = rows.Select(x => (x.Kind, x.Total)).ToArray();
        return new PackageUtilizationMetrics(
            Sum(mapped, BenefitTransactionKind.Grant),
            Sum(mapped, BenefitTransactionKind.Redeem),
            Sum(mapped, BenefitTransactionKind.Forfeit));
    }

    private async Task<LoyaltyPointsMetrics> LoyaltyMetricsAsync(
        Guid tenantId,
        RetentionAnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        var tx = db.Set<LoyaltyTransaction>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.OccurredAt >= query.From && x.OccurredAt <= query.To);

        var grouped = await tx
            .GroupBy(x => x.Kind)
            .Select(g => new { Kind = g.Key, Points = g.Sum(x => x.Points) })
            .ToListAsync(cancellationToken);

        var outstanding = await db.Set<LoyaltyAccount>().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .SumAsync(x => x.PointsBalance, cancellationToken);

        static int PositiveSum(IEnumerable<(LoyaltyTransactionKind Kind, int Points)> source, LoyaltyTransactionKind kind) =>
            source.Where(x => x.Kind == kind).Sum(x => Math.Max(0, x.Points));

        static int NegativeSum(IEnumerable<(LoyaltyTransactionKind Kind, int Points)> source, LoyaltyTransactionKind kind) =>
            source.Where(x => x.Kind == kind).Sum(x => Math.Max(0, -x.Points));

        var mapped = grouped.Select(x => (x.Kind, x.Points)).ToArray();
        return new LoyaltyPointsMetrics(
            PositiveSum(mapped, LoyaltyTransactionKind.Earn) + PositiveSum(mapped, LoyaltyTransactionKind.Adjust),
            NegativeSum(mapped, LoyaltyTransactionKind.Redeem),
            NegativeSum(mapped, LoyaltyTransactionKind.Expire),
            outstanding);
    }
}
