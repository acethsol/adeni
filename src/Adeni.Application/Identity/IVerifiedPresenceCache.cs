namespace Adeni.Application.Identity;

using Adeni.Domain.Identity;

/// <summary>Write-through cache for active verified presence (no coordinates).</summary>
public interface IVerifiedPresenceCache
{
    Task<VerifiedPresenceCacheEntry?> TryGetActiveAsync(
        Guid tenantId,
        Guid businessUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task SetFromSessionAsync(
        VerifiedPresenceSession session,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        Guid tenantId,
        Guid businessUserId,
        CancellationToken cancellationToken = default);
}

public sealed record VerifiedPresenceCacheEntry(
    Guid SessionId,
    Guid BusinessLocationId,
    DateTimeOffset ExpiresAt);
