namespace Adeni.Infrastructure.Catalog;

using Adeni.Application.Caching;
using Adeni.Application.Catalog;

public sealed class CategoryService(ICacheService cache) : ICategoryService
{
    private static readonly IReadOnlyList<CategoryResponse> Categories =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111101"), "Barbers", "barbers", "beauty"),
        new(Guid.Parse("11111111-1111-1111-1111-111111111102"), "Hair Salons", "hair-salons", "beauty"),
        new(Guid.Parse("11111111-1111-1111-1111-111111111103"), "Nail & Spa", "nail-spa", "beauty"),
        new(Guid.Parse("11111111-1111-1111-1111-111111111104"), "Makeup & Brows", "makeup-brows", "beauty"),
        new(Guid.Parse("22222222-2222-2222-2222-222222222201"), "Plumbers", "plumbers", "home-services"),
        new(Guid.Parse("22222222-2222-2222-2222-222222222202"), "Electricians", "electricians", "home-services"),
        new(Guid.Parse("22222222-2222-2222-2222-222222222203"), "Cleaning", "cleaning", "home-services")
    ];

    public Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(
        CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync(
            CacheKeys.CategoriesAll,
            CacheTtl.Categories,
            _ => Task.FromResult(Categories),
            cancellationToken);
}
