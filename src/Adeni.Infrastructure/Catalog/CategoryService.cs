namespace Adeni.Infrastructure.Catalog;

using Adeni.Application.Caching;
using Adeni.Application.Catalog;

public sealed class CategoryService(ICacheService cache) : ICategoryService
{
    private static readonly IReadOnlyList<CategoryResponse> BeautyCategories =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111101"), "Barbers", "barbers", "beauty"),
        new(Guid.Parse("11111111-1111-1111-1111-111111111102"), "Hair Salons", "hair-salons", "beauty"),
        new(Guid.Parse("11111111-1111-1111-1111-111111111103"), "Nail & Spa", "nail-spa", "beauty"),
        new(Guid.Parse("11111111-1111-1111-1111-111111111104"), "Makeup & Brows", "makeup-brows", "beauty")
    ];

    public Task<IReadOnlyList<CategoryResponse>> GetBeautyCategoriesAsync(
        CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync(
            CacheKeys.CategoriesAll,
            CacheTtl.Categories,
            _ => Task.FromResult(BeautyCategories),
            cancellationToken);
}
