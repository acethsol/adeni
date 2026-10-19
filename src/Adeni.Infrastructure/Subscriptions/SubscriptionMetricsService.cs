namespace Adeni.Infrastructure.Subscriptions;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Subscriptions;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class SubscriptionMetricsService(
    AdeniDbContext dbContext,
    ITenantPricingResolver tenantPricingResolver) : ISubscriptionMetricsService
{
    private static readonly HashSet<string> PaidChargeStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "paid",
        "success",
        "completed",
    };

    public async Task<AdminSubscriptionMetricsResponse> GetAdminMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        var asOf = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(asOf.Year, asOf.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var tenants = await dbContext.Tenants.AsNoTracking().ToListAsync(cancellationToken);

        var paidCount = tenants.Count(IsPayingTenant);
        var pastDueCount = tenants.Count(t => t.BillingStatus == TenantBillingStatus.PastDue);
        var trialingCount = tenants.Count(t => t.BillingStatus == TenantBillingStatus.Trialing);

        var skuMix = Enum.GetValues<SubscriptionSku>()
            .Select(sku => new AdminSubscriptionSkuCount(
                SubscriptionSkuMapping.ToApiValue(sku),
                tenants.Count(t => t.SubscriptionSku == sku)))
            .ToList();

        var synced = await dbContext.TenantSubscriptionCharges
            .AsNoTracking()
            .Where(x => x.OccurredAt >= monthStart && x.AmountMinor != null)
            .Where(x => PaidChargeStatuses.Contains(x.Status))
            .GroupBy(x => x.Currency ?? "NGN")
            .Select(g => new AdminSubscriptionMetricsCurrencyTotal(
                g.Key,
                g.Sum(x => x.AmountMinor!.Value)))
            .ToListAsync(cancellationToken);

        var countryByTenant = await tenantPricingResolver.ResolvePrimaryBillingCountryByTenantAsync(cancellationToken);

        var estimatedByCurrency = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var tenant in tenants.Where(IsPayingTenant))
        {
            countryByTenant.TryGetValue(tenant.Id, out var country);
            country ??= SubscriptionCatalogPrices.DefaultCountryCode;
            var pricing = tenantPricingResolver.ResolveForTenant(tenant, country);
            var minor = tenantPricingResolver.ToAmountMinor(pricing.EffectiveMonthlyAmountMajor, pricing.Currency);
            if (minor is > 0)
            {
                estimatedByCurrency[pricing.Currency] =
                    estimatedByCurrency.GetValueOrDefault(pricing.Currency) + minor.Value;
            }
        }

        var estimated = estimatedByCurrency
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => new AdminSubscriptionMetricsCurrencyTotal(x.Key, x.Value))
            .ToList();

        var syncedTotal = synced.Sum(x => x.AmountMinor);
        var mrrSource = syncedTotal > 0 ? "synced_charges" : "estimated_list_price";

        return new AdminSubscriptionMetricsResponse(
            paidCount,
            pastDueCount,
            trialingCount,
            skuMix,
            synced,
            estimated,
            mrrSource,
            asOf);
    }

    private static bool IsPayingTenant(Domain.Tenancy.Tenant tenant) =>
        tenant.BillingStatus == TenantBillingStatus.Active
        && tenant.SubscriptionSku is SubscriptionSku.Professional or SubscriptionSku.Business;
}
