namespace Adeni.Infrastructure.Finance;

using Adeni.Application.Markets;
using Adeni.Application.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class PaymentProviderRegistry(
    AdeniDbContext dbContext,
    IMarketCatalog marketCatalog,
    PaystackPaymentProvider paystack,
    StripePaymentProvider stripe,
    StubPaymentProvider stub) : IPaymentProviderRegistry
{
    public IPaymentProvider GetCommerceProvider(string providerKey) =>
        MarketPaymentProviders.Normalize(providerKey) switch
        {
            MarketPaymentProviders.Stripe => stripe,
            MarketPaymentProviders.Stub => stub,
            _ => paystack,
        };

    public async Task<string> ResolveCommerceProviderKeyForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var marketId = await ResolvePrimaryMarketIdAsync(tenantId, cancellationToken);
        return ResolveCommerceProviderKeyForMarket(marketId);
    }

    public async Task<string> ResolveBillingProviderKeyForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var marketId = await ResolvePrimaryMarketIdAsync(tenantId, cancellationToken);
        return ResolveBillingProviderKeyForMarket(marketId);
    }

    public string ResolveCommerceProviderKeyForCountry(string? countryCode)
    {
        var normalized = NormalizeCountry(countryCode);
        var market = marketCatalog.List()
            .FirstOrDefault(m => string.Equals(m.CountryCode, normalized, StringComparison.OrdinalIgnoreCase));
        return market is null
            ? FallbackCommerceForCountry(normalized)
            : MarketPaymentProviders.Normalize(market.DefaultCommerceProvider);
    }

    public string ResolveBillingProviderKeyForCountry(string? countryCode)
    {
        var normalized = NormalizeCountry(countryCode);
        var market = marketCatalog.List()
            .FirstOrDefault(m => string.Equals(m.CountryCode, normalized, StringComparison.OrdinalIgnoreCase));
        return market is null
            ? MarketPaymentProviders.Paystack
            : MarketPaymentProviders.Normalize(market.DefaultBillingProvider);
    }

    public string ResolveCommerceProviderKeyForMarket(string? marketId)
    {
        var market = marketCatalog.GetById(marketId ?? string.Empty);
        return market is null
            ? MarketPaymentProviders.Paystack
            : MarketPaymentProviders.Normalize(market.DefaultCommerceProvider);
    }

    public string ResolveBillingProviderKeyForMarket(string? marketId)
    {
        var market = marketCatalog.GetById(marketId ?? string.Empty);
        return market is null
            ? MarketPaymentProviders.Paystack
            : MarketPaymentProviders.Normalize(market.DefaultBillingProvider);
    }

    private static string FallbackCommerceForCountry(string countryCode) =>
        countryCode is "CA" or "US"
            ? MarketPaymentProviders.Stripe
            : MarketPaymentProviders.Paystack;

    private static string NormalizeCountry(string? countryCode) =>
        string.IsNullOrWhiteSpace(countryCode)
            ? "NG"
            : countryCode.Trim().ToUpperInvariant();

    private async Task<string?> ResolvePrimaryMarketIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var marketId = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsPrimary && x.IsActive)
            .Select(x => x.MarketId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(marketId))
        {
            return marketId;
        }

        return await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.MarketId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
