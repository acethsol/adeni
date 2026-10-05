namespace Adeni.Application.Catalog;

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? ParentSlug);

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(
        CategoryListQuery? query = null,
        CancellationToken cancellationToken = default);

    string NormalizeSlug(string slug);

    bool IsKnownSlug(string slug);

    IReadOnlyList<string> GetDiscoveryMatchSlugs(string filterSlug);

    IReadOnlyList<ServiceTemplateResponse> GetServiceTemplates(string categorySlug);
}
