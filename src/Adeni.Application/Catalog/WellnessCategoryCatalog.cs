namespace Adeni.Application.Catalog;

public sealed record CategoryListQuery(
    string? MarketId = null,
    bool WellnessScope = true,
    bool IncludeDisabled = false);

public interface IWellnessCategoryCatalog
{
    IReadOnlyList<CategoryResponse> ListCategories(CategoryListQuery query);

    bool IsKnownSlug(string slug);

    string NormalizeSlug(string slug);

    /// <summary>Primary slug plus legacy aliases — for discovery category filters.</summary>
    IReadOnlyList<string> GetDiscoveryMatchSlugs(string filterSlug);

    IReadOnlyList<ServiceTemplateResponse> GetServiceTemplates(string categorySlug);
}

public sealed record ServiceTemplateResponse(
    string Id,
    string Name,
    int DefaultDurationMinutes,
    string BookingDeliveryType);
