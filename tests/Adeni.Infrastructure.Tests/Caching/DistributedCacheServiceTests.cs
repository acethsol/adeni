namespace Adeni.Infrastructure.Tests.Caching;

using Adeni.Application.Caching;
using Adeni.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

public sealed class DistributedCacheServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_returns_cached_value_on_second_call()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddSingleton<ICacheService, DistributedCacheService>();
        using var provider = services.BuildServiceProvider();

        var cache = provider.GetRequiredService<ICacheService>();
        var factoryCalls = 0;

        var first = await cache.GetOrCreateAsync(
            "test:key",
            TimeSpan.FromMinutes(1),
            _ =>
            {
                factoryCalls++;
                return Task.FromResult("value");
            });

        var second = await cache.GetOrCreateAsync(
            "test:key",
            TimeSpan.FromMinutes(1),
            _ =>
            {
                factoryCalls++;
                return Task.FromResult("other");
            });

        Assert.Equal("value", first);
        Assert.Equal("value", second);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task RemoveAsync_evicts_cached_value()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddSingleton<ICacheService, DistributedCacheService>();
        using var provider = services.BuildServiceProvider();

        var cache = provider.GetRequiredService<ICacheService>();
        await cache.SetAsync("remove:key", "payload", TimeSpan.FromMinutes(1));
        await cache.RemoveAsync("remove:key");

        var value = await cache.GetAsync<string>("remove:key");

        Assert.Null(value);
    }
}
