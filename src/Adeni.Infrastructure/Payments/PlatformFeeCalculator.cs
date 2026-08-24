namespace Adeni.Infrastructure.Payments;

using Adeni.Application.Markets;
using Adeni.Application.Payments;

public sealed class PlatformFeeCalculator(IMarketCatalog marketCatalog) : IPlatformFeeCalculator
{
    public decimal CalculateFee(decimal amount, string marketId)
    {
        var percent = GetFeePercent(marketId);
        return Math.Round(amount * percent / 100m, 2, MidpointRounding.AwayFromZero);
    }

    public decimal GetFeePercent(string marketId)
    {
        var market = marketCatalog.GetById(marketId);
        return market?.PlatformFeePercent ?? 0m;
    }
}
