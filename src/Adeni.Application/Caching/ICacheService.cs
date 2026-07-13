namespace Adeni.Application.Caching;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string? ConnectionString { get; set; }

    public bool Enabled => !string.IsNullOrWhiteSpace(ConnectionString);
}

public static class CacheTtl
{
    public static readonly TimeSpan TenantProfile = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Discovery = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan Categories = TimeSpan.FromHours(1);
    public static readonly TimeSpan Markets = TimeSpan.FromHours(1);
    public static readonly TimeSpan SlotLock = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan Translation = TimeSpan.FromDays(90);
}

public static class CacheKeys
{
    public static string LocationProfile(string slug) => $"location:{slug}:profile";
    public static string TenantProfile(Guid tenantId) => $"tenant:{tenantId:N}:profile";
    public static string Discovery(
        double lat,
        double lng,
        string? category,
        string? marketId,
        int page,
        int pageSize,
        string? query,
        string sort,
        int? minRating = null) =>
        $"discovery:{lat:F3}:{lng:F3}:{category ?? "all"}:{marketId ?? "any"}:{page}:{pageSize}:{query ?? ""}:{sort}:{minRating ?? 0}";
    public static string CategoriesAll => "categories:all";
    public static string MarketsAll => "markets:all";
    public static string Translation(string source, string target, string textHash) =>
        $"translation:v1:{source}:{target}:{textHash}";
    public static string SlotLock(Guid tenantId, DateTimeOffset start, Guid serviceId) =>
        $"slot-lock:{tenantId:N}:{start.UtcDateTime:O}:{serviceId:N}";
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class;

    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
        where T : class;

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
        where T : class;
}

public interface IDistributedLockProvider
{
    Task<IAsyncDisposable?> TryAcquireAsync(
        string resourceKey,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);
}

public interface IRedisHealthCheck
{
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
}
