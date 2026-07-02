namespace Adeni.Application.Markets;

/// <summary>
/// Allowed market ids — keep in sync with packages/shared/src/markets.ts.
/// </summary>
public static class KnownMarketCatalog
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "lagos",
        "abuja",
        "ottawa",
        "toronto",
        "houston",
        "dallas",
    };

    public static bool IsValid(string? marketId) =>
        !string.IsNullOrWhiteSpace(marketId) && All.Contains(marketId.Trim());

    public static string Normalize(string marketId) => marketId.Trim().ToLowerInvariant();
}
