namespace Adeni.Infrastructure.Caching;

using System.Text.Json;
using Adeni.Application.Caching;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

public sealed class DistributedCacheService(IDistributedCache cache) : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        var payload = await cache.GetStringAsync(key, cancellationToken);
        return payload is null ? null : JsonSerializer.Deserialize<T>(payload, SerializerOptions);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var payload = JsonSerializer.Serialize(value, SerializerOptions);
        return cache.SetStringAsync(
            key,
            payload,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(key, cancellationToken);

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var created = await factory(cancellationToken);
        await SetAsync(key, created, ttl, cancellationToken);
        return created;
    }
}

public sealed class RedisLockProvider(IConnectionMultiplexer multiplexer) : IDistributedLockProvider
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string resourceKey,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        var database = multiplexer.GetDatabase();
        var lockValue = Guid.NewGuid().ToString("N");
        var acquired = await database.StringSetAsync(
            resourceKey,
            lockValue,
            expiry,
            when: StackExchange.Redis.When.NotExists);

        return acquired ? new RedisLockHandle(database, resourceKey, lockValue) : null;
    }

    private sealed class RedisLockHandle(
        IDatabase database,
        string key,
        string value) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            const string script = """
                if redis.call('get', KEYS[1]) == ARGV[1] then
                  return redis.call('del', KEYS[1])
                else
                  return 0
                end
                """;

            _ = database.ScriptEvaluateAsync(script, [key], [value]);
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class RedisHealthCheck(IConnectionMultiplexer multiplexer) : IRedisHealthCheck
{
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var latency = await multiplexer.GetDatabase().PingAsync();
        return latency >= TimeSpan.Zero;
    }
}

public sealed class UnconfiguredRedisHealthCheck : IRedisHealthCheck
{
    public Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult(false);
    }
}

public sealed class NoOpLockProvider : IDistributedLockProvider
{
    public Task<IAsyncDisposable?> TryAcquireAsync(
        string resourceKey,
        TimeSpan expiry,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IAsyncDisposable?>(new NoOpLockHandle());

    private sealed class NoOpLockHandle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
