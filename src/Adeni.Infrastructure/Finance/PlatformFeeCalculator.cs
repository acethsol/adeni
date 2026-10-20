namespace Adeni.Infrastructure.Finance;

using Adeni.Application.Markets;
using Adeni.Application.Finance;
using Adeni.Application.Subscriptions;

public sealed class PlatformFeeCalculator(
    IMarketCatalog marketCatalog,
    IPricingCatalogService pricingCatalog) : IPlatformFeeCalculator
{
    public decimal CalculateFee(decimal amount, string marketId)
    {
        var percent = GetFeePercent(marketId);
        return Math.Round(amount * percent / 100m, 2, MidpointRounding.AwayFromZero);
    }

    public decimal GetFeePercent(string marketId)
    {
        var market = marketCatalog.GetById(marketId);
        if (market is null)
        {
            return 0m;
        }

        return pricingCatalog.GetPlatformFeePercent(market.CountryCode);
    }
}
