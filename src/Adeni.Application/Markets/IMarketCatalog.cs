namespace Adeni.Application.Markets;

public sealed record MarketLocation(double Lat, double Lng);

public sealed record MarketDefinition(
    string Id,
    string Name,
    string CountryCode,
    string Currency,
    string TimeZoneId,
    MarketLocation DefaultLocation,
    IReadOnlyList<string> Languages,
    bool IsLive,
    string? LaunchNote = null,
    decimal PlatformFeePercent = 0m,
    string DefaultCommerceProvider = "paystack",
    string DefaultBillingProvider = "paystack");

public static class MarketPaymentProviders
{
    public const string Paystack = "paystack";
    public const string Stripe = "stripe";
    public const string Stub = "stub";

    public static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Paystack
            : value.Trim().ToLowerInvariant();
}

public interface IMarketCatalog
{
    IReadOnlyList<MarketDefinition> List();

    IReadOnlyList<MarketDefinition> ListLive();

    MarketDefinition? GetById(string marketId);

    bool IsValid(string? marketId);

    string Normalize(string marketId);
}
