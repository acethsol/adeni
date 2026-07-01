namespace Adeni.Infrastructure.Tests.Caching;

using Adeni.Application.Caching;
using Adeni.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

public sealed class CacheServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAdeniCaching_uses_memory_fallback_in_development_without_redis()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        services.AddAdeniCaching(configuration, new TestHostEnvironment());

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ICacheService>());
        Assert.NotNull(provider.GetService<IDistributedLockProvider>());
        Assert.IsType<NoOpLockProvider>(provider.GetRequiredService<IDistributedLockProvider>());
    }

    [Fact]
    public void AddAdeniCaching_registers_redis_implementations_when_configured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = "localhost:6379"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddAdeniCaching(configuration, new TestHostEnvironment());

        var lockDescriptor = services.Single(d => d.ServiceType == typeof(IDistributedLockProvider));
        Assert.Equal(typeof(RedisLockProvider), lockDescriptor.ImplementationType);
        Assert.Contains(services, d => d.ServiceType == typeof(ICacheService));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Adeni.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
