namespace Adeni.Infrastructure.Admin;

using Adeni.Application.Admin;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class AdminRevenueMetricsService(AdeniDbContext dbContext) : IAdminRevenueMetricsService
{
    private static readonly HashSet<string> PaidChargeStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "paid",
        "success",
        "completed",
    };

    public async Task<AdminRevenueMetricsResponse> GetMetricsAsync(
        string granularity,
        CancellationToken cancellationToken = default)
    {
        var asOf = DateTimeOffset.UtcNow;
        var byYear = string.Equals(granularity, "year", StringComparison.OrdinalIgnoreCase);
        var windowStart = byYear
            ? new DateTimeOffset(asOf.Year - 4, 1, 1, 0, 0, 0, TimeSpan.Zero)
            : asOf.AddMonths(-11);

        var tenantNames = await dbContext.Tenants
            .AsNoTracking()
            .Select(t => new { t.Id, t.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var saasRows = await dbContext.TenantSubscriptionCharges
            .AsNoTracking()
            .Where(x => x.OccurredAt >= windowStart && x.AmountMinor != null)
            .Where(x => PaidChargeStatuses.Contains(x.Status))
            .ToListAsync(cancellationToken);

        var commerceRows = await dbContext.PaymentIntents
            .AsNoTracking()
            .Where(x => x.Status == PaymentIntentStatus.Completed && x.UpdatedAt >= windowStart)
            .ToListAsync(cancellationToken);

        var bucketIndex = new Dictionary<string, AdminRevenuePeriodBucket>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in saasRows)
        {
            var currency = row.Currency ?? "NGN";
            var key = BucketKey(byYear, row.OccurredAt, currency);
            UpsertBucket(bucketIndex, key, byYear, row.OccurredAt, currency, row.AmountMinor!.Value, 0m, 0m);
        }

        foreach (var row in commerceRows)
        {
            var currency = row.Currency ?? "NGN";
            var key = BucketKey(byYear, row.UpdatedAt, currency);
            UpsertBucket(bucketIndex, key, byYear, row.UpdatedAt, currency, 0, row.Amount, row.PlatformFeeAmount);
        }

        var buckets = bucketIndex.Values
            .OrderByDescending(x => x.Period)
            .ThenBy(x => x.Currency, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var recentSaas = saasRows
            .OrderByDescending(x => x.OccurredAt)
            .Take(25)
            .Select(x => new AdminSaasChargeSummary(
                x.Id,
                x.TenantId,
                tenantNames.GetValueOrDefault(x.TenantId) ?? "—",
                x.ProviderReference,
                x.EventType,
                x.SubscriptionSku is { } sku ? SubscriptionSkuMapping.ToApiValue(sku) : null,
                x.AmountMinor,
                x.Currency,
                x.Status,
                x.OccurredAt))
            .ToList();

        var recentCommerce = commerceRows
            .OrderByDescending(x => x.UpdatedAt)
            .Take(25)
            .Select(x => new AdminCommercePaymentSummary(
                x.Id,
                x.TenantId,
                tenantNames.GetValueOrDefault(x.TenantId) ?? "—",
                x.Amount,
                x.PlatformFeeAmount,
                x.Currency,
                x.Status.ToString(),
                x.UpdatedAt))
            .ToList();

        return new AdminRevenueMetricsResponse(
            byYear ? "year" : "month",
            buckets,
            recentSaas,
            recentCommerce,
            asOf);
    }

    private static string BucketKey(bool byYear, DateTimeOffset at, string currency) =>
        $"{(byYear ? at.Year.ToString() : at.ToString("yyyy-MM"))}|{currency}";

    private static void UpsertBucket(
        Dictionary<string, AdminRevenuePeriodBucket> index,
        string key,
        bool byYear,
        DateTimeOffset at,
        string currency,
        long saasMinorDelta,
        decimal commerceVolumeDelta,
        decimal platformFeeDelta)
    {
        var period = byYear ? at.Year.ToString() : at.ToString("yyyy-MM");
        if (index.TryGetValue(key, out var existing))
        {
            index[key] = existing with
            {
                SaasAmountMinor = existing.SaasAmountMinor + saasMinorDelta,
                CommerceVolume = existing.CommerceVolume + commerceVolumeDelta,
                PlatformFeeTotal = existing.PlatformFeeTotal + platformFeeDelta,
            };
            return;
        }

        index[key] = new AdminRevenuePeriodBucket(
            period,
            currency,
            saasMinorDelta,
            commerceVolumeDelta,
            platformFeeDelta);
    }
}
