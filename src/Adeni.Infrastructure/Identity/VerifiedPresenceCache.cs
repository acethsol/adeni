namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Caching;
using Adeni.Application.Identity;
using Adeni.Domain.Identity;

public sealed class VerifiedPresenceCache(ICacheService cache, TimeProvider clock) : IVerifiedPresenceCache
{
    public async Task<VerifiedPresenceCacheEntry?> TryGetActiveAsync(
        Guid tenantId,
        Guid businessUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var key = CacheKeys.TenantVerifiedPresence(tenantId, businessUserId);
        var entry = await cache.GetAsync<VerifiedPresenceCacheEntry>(key, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        if (entry.ExpiresAt <= now)
        {
            await cache.RemoveAsync(key, cancellationToken);
            return null;
        }

        return entry;
    }

    public Task SetFromSessionAsync(
        VerifiedPresenceSession session,
        CancellationToken cancellationToken = default)
    {
        if (session.InvalidatedAt is not null)
        {
            return RemoveAsync(session.TenantId, session.BusinessUserId, cancellationToken);
        }

        var now = clock.GetUtcNow();
        var ttl = session.ExpiresAt - now;
        if (ttl <= TimeSpan.Zero)
        {
            return RemoveAsync(session.TenantId, session.BusinessUserId, cancellationToken);
        }

        var entry = new VerifiedPresenceCacheEntry(
            session.Id,
            session.BusinessLocationId,
            session.ExpiresAt);

        return cache.SetAsync(
            CacheKeys.TenantVerifiedPresence(session.TenantId, session.BusinessUserId),
            entry,
            ttl,
            cancellationToken);
    }

    public Task RemoveAsync(
        Guid tenantId,
        Guid businessUserId,
        CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(CacheKeys.TenantVerifiedPresence(tenantId, businessUserId), cancellationToken);
}
