namespace Adeni.Infrastructure.Markets;

using Adeni.Application.Markets;

public sealed class MarketCatalogState
{
    private IReadOnlyList<MarketDefinition> _markets = [];
    private HashSet<string> _ids = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<MarketDefinition> Markets => _markets;

    public void Update(IReadOnlyList<MarketDefinition> markets)
    {
        _markets = markets;
        _ids = new HashSet<string>(markets.Select(market => market.Id), StringComparer.OrdinalIgnoreCase);
    }

    public bool IsValid(string? marketId) =>
        !string.IsNullOrWhiteSpace(marketId) && _ids.Contains(marketId.Trim());

    public string Normalize(string marketId) => marketId.Trim().ToLowerInvariant();
}
