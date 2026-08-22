namespace Adeni.Application.Catalog;

using Adeni.Domain.Tenancy;

public sealed class CategoryWorkflowCatalog
{
    public required BusinessType DefaultBusinessType { get; init; }

    public required IReadOnlyDictionary<string, CategoryWorkflowEntry> Categories { get; init; }

    public required IReadOnlyDictionary<BusinessType, BusinessTypeWorkflowEntry> BusinessTypes { get; init; }

    public required IReadOnlyList<string> AppointmentFallbackCapabilities { get; init; }

    public BusinessType GetDefaultBusinessType(string categorySlug)
    {
        var slug = categorySlug.Trim();
        if (Categories.TryGetValue(slug, out var entry) && entry.DefaultBusinessType.HasValue)
        {
            return entry.DefaultBusinessType.Value;
        }

        return DefaultBusinessType;
    }

    public IReadOnlyList<string> GetCapabilities(BusinessType businessType, string categorySlug)
    {
        if (businessType == BusinessType.QuoteRequest
            && BusinessTypes.TryGetValue(BusinessType.QuoteRequest, out var quoteEntry)
            && quoteEntry.Capabilities.Count > 0)
        {
            return quoteEntry.Capabilities;
        }

        var slug = categorySlug.Trim();
        if (Categories.TryGetValue(slug, out var categoryEntry) && categoryEntry.Capabilities.Count > 0)
        {
            return categoryEntry.Capabilities;
        }

        return AppointmentFallbackCapabilities;
    }

    public string GetDiscoveryCta(BusinessType businessType) =>
        BusinessTypes.TryGetValue(businessType, out var entry)
            ? entry.DiscoveryCta
            : DiscoveryCta.BookNow;
}

public sealed class CategoryWorkflowEntry
{
    public BusinessType? DefaultBusinessType { get; init; }

    public IReadOnlyList<string> Capabilities { get; init; } = [];
}

public sealed class BusinessTypeWorkflowEntry
{
    public required string DiscoveryCta { get; init; }

    public IReadOnlyList<string> Capabilities { get; init; } = [];
}
