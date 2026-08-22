namespace Adeni.Application.Catalog;

using Adeni.Domain.Tenancy;

public static class BusinessCapability
{
    public const string Calendar = "calendar";
    public const string FixedPricing = "fixed_pricing";
    public const string Quotes = "quotes";
    public const string OnSiteAddress = "on_site_address";
    public const string PhotoUpload = "photo_upload";
    public const string LicenseBadge = "license_badge";
    public const string Deposits = "deposits";
    public const string Reviews = "reviews";
    public const string MultiService = "multi_service";
    public const string ServiceArea = "service_area";
}

public static class DiscoveryCta
{
    public const string BookNow = "book_now";
    public const string GetQuote = "get_quote";
}

public interface IBusinessCapabilitiesService
{
    BusinessType GetDefaultBusinessType(string categorySlug);

    IReadOnlyList<string> GetCapabilities(BusinessType businessType, string categorySlug);

    string GetDiscoveryCta(BusinessType businessType);

    bool HasCapability(BusinessType businessType, string categorySlug, string capability);
}
