namespace Adeni.Application.Catalog;

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? ParentSlug);

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryResponse>> GetBeautyCategoriesAsync(
        CancellationToken cancellationToken = default);
}
