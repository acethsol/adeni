namespace Adeni.Infrastructure.Tests.Catalog;

using Adeni.Application.Caching;
using Adeni.Infrastructure.Catalog;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

public sealed class CategoryServiceTests
{
    [Fact]
    public async Task GetCategoriesAsync_uses_categories_cache_key()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddSingleton<ICacheService, Adeni.Infrastructure.Caching.DistributedCacheService>();
        services.AddCategoryWorkflowCatalog();
        using var provider = services.BuildServiceProvider();

        var cache = provider.GetRequiredService<IDistributedCache>();
        var categories = provider.GetRequiredService<Application.Catalog.ICategoryService>();

        var first = await categories.GetCategoriesAsync();
        var second = await categories.GetCategoriesAsync();

        Assert.Equal(8, first.Count); // enabled beauty/wellness leaf categories (incl. pilates)
        Assert.Contains(first, c => c.Slug == "hair-grooming");
        Assert.Contains(first, c => c.Slug == "pilates");
        Assert.DoesNotContain(first, c => c.Slug == "plumbers");
        Assert.Equal(first, second);
        Assert.NotNull(await cache.GetStringAsync("categories:all:True:False:all"));
    }
}
