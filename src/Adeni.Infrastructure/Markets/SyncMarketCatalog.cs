namespace Adeni.Infrastructure.Markets;

using Adeni.Application.Markets;

public sealed class SyncMarketCatalog(MarketCatalogState state) : IMarketCatalog
{
    public IReadOnlyList<MarketDefinition> List() => state.Markets;

    public IReadOnlyList<MarketDefinition> ListLive() =>
        state.Markets.Where(market => market.IsLive).ToArray();

    public MarketDefinition? GetById(string marketId)
    {
        if (string.IsNullOrWhiteSpace(marketId))
        {
            return null;
        }

        return state.Markets.FirstOrDefault(market =>
            string.Equals(market.Id, marketId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public bool IsValid(string? marketId) => state.IsValid(marketId);

    public string Normalize(string marketId) => state.Normalize(marketId);
}
