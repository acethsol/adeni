namespace Adeni.Infrastructure.Tests.Catalog;

using Adeni.Application.Caching;
using Adeni.Infrastructure.Catalog;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

public sealed class CategoryServiceTests
{
    [Fact]
    public async Task GetBeautyCategoriesAsync_uses_categories_cache_key()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddSingleton<ICacheService, Adeni.Infrastructure.Caching.DistributedCacheService>();
        services.AddSingleton<Application.Catalog.ICategoryService, CategoryService>();
        using var provider = services.BuildServiceProvider();

        var cache = provider.GetRequiredService<IDistributedCache>();
        var categories = provider.GetRequiredService<Application.Catalog.ICategoryService>();

        var first = await categories.GetBeautyCategoriesAsync();
        var second = await categories.GetBeautyCategoriesAsync();

        Assert.Equal(4, first.Count);
        Assert.Equal(first, second);
        Assert.NotNull(await cache.GetStringAsync("categories:all"));
    }
}
