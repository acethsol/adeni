namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Subscriptions;

/// <summary>
/// SaaS list prices (major currency units). Keep aligned with packages/shared/data/country-pricing.json.
/// </summary>
public static class SubscriptionCatalogPrices
{
    private static readonly Dictionary<string, Dictionary<SubscriptionSku, decimal>> ByCountry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NG"] = new()
        {
            [SubscriptionSku.Starter] = 35_000m,
            [SubscriptionSku.Professional] = 55_000m,
            [SubscriptionSku.Business] = 95_000m,
        },
        ["CA"] = new()
        {
            [SubscriptionSku.Starter] = 99m,
            [SubscriptionSku.Professional] = 149m,
            [SubscriptionSku.Business] = 249m,
        },
    };

    private static readonly Dictionary<string, string> CurrencyByCountry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NG"] = "NGN",
        ["CA"] = "CAD",
        ["US"] = "USD",
    };

    public const string DefaultCountryCode = "NG";

    public static string CurrencyForCountry(string? countryCode)
    {
        var key = NormalizeCountry(countryCode);
        return CurrencyByCountry.TryGetValue(key, out var currency) ? currency : "NGN";
    }

    public static decimal? GetMonthlyMajor(string? countryCode, SubscriptionSku sku)
    {
        var key = NormalizeCountry(countryCode);
        if (string.Equals(key, "US", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!ByCountry.TryGetValue(key, out var skus))
        {
            return ByCountry[DefaultCountryCode].TryGetValue(sku, out var fallback) ? fallback : null;
        }

        return skus.TryGetValue(sku, out var amount) ? amount : null;
    }

    /// <summary>Minor units for ledger rows (NGN has no fractional minor; CAD/USD × 100).</summary>
    public static long? ToAmountMinor(string? countryCode, SubscriptionSku sku)
    {
        var major = GetMonthlyMajor(countryCode, sku);
        if (major is null)
        {
            return null;
        }

        var currency = CurrencyForCountry(countryCode);
        return ToAmountMinorFromMajor(major.Value, currency);
    }

    public static long? ToAmountMinorFromMajor(decimal amountMajor, string currency)
    {
        if (amountMajor <= 0)
        {
            return null;
        }

        return string.Equals(currency, "NGN", StringComparison.OrdinalIgnoreCase)
            ? (long)amountMajor
            : (long)(amountMajor * 100m);
    }

    private static string NormalizeCountry(string? countryCode) =>
        string.IsNullOrWhiteSpace(countryCode)
            ? DefaultCountryCode
            : countryCode.Trim().ToUpperInvariant();
}
