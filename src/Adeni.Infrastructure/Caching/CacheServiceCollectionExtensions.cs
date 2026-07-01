namespace Adeni.Infrastructure.Caching;

using Adeni.Application.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

public static class CacheServiceCollectionExtensions
{
    public static IServiceCollection AddAdeniCaching(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName));

        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

        if (redisOptions.Enabled)
        {
            var configurationOptions = ConfigurationOptions.Parse(redisOptions.ConnectionString!);
            configurationOptions.AbortOnConnectFail = false;

            services.AddStackExchangeRedisCache(options =>
            {
                options.ConfigurationOptions = configurationOptions;
                options.InstanceName = "adeni:";
            });

            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configurationOptions));
            services.AddSingleton<IDistributedLockProvider, RedisLockProvider>();
            services.AddSingleton<IRedisHealthCheck, RedisHealthCheck>();
        }
        else if (environment.IsDevelopment() || environment.EnvironmentName == "Testing")
        {
            services.AddDistributedMemoryCache();
            services.AddSingleton<IDistributedLockProvider, NoOpLockProvider>();
            services.AddSingleton<IRedisHealthCheck, UnconfiguredRedisHealthCheck>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Redis connection string is required in '{environment.EnvironmentName}'. Set Redis:ConnectionString.");
        }

        services.AddSingleton<ICacheService, DistributedCacheService>();
        return services;
    }
}
