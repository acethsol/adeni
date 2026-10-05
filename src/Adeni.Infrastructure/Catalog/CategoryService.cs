namespace Adeni.Infrastructure.Catalog;

using Adeni.Application.Caching;
using Adeni.Application.Catalog;

public sealed class CategoryService(
    ICacheService cache,
    IWellnessCategoryCatalog wellnessCatalog) : ICategoryService
{
    public Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(
        CategoryListQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        query ??= new CategoryListQuery();
        var marketKey = string.IsNullOrWhiteSpace(query.MarketId)
            ? "all"
            : query.MarketId.Trim().ToLowerInvariant();
        var cacheKey = $"{CacheKeys.CategoriesAll}:{query.WellnessScope}:{query.IncludeNonV1}:{marketKey}";

        return cache.GetOrCreateAsync(
            cacheKey,
            CacheTtl.Categories,
            _ => Task.FromResult(wellnessCatalog.ListCategories(query)),
            cancellationToken);
    }

    public string NormalizeSlug(string slug) => wellnessCatalog.NormalizeSlug(slug);

    public bool IsKnownSlug(string slug) => wellnessCatalog.IsKnownSlug(slug);

    public IReadOnlyList<string> GetDiscoveryMatchSlugs(string filterSlug) =>
        wellnessCatalog.GetDiscoveryMatchSlugs(filterSlug);

    public IReadOnlyList<ServiceTemplateResponse> GetServiceTemplates(string categorySlug) =>
        wellnessCatalog.GetServiceTemplates(categorySlug);
}
