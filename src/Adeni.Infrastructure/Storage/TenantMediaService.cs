namespace Adeni.Infrastructure.Storage;

using Adeni.Application.Caching;
using Adeni.Application.Storage;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class TenantMediaService(
    AdeniDbContext dbContext,
    IFileStorage fileStorage,
    ICacheService cache) : ITenantMediaService
{
    private const long MaxCoverBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    public async Task<Result<MediaUploadUrlResponse>> CreateUploadUrlAsync(
        Guid tenantId,
        string auth0Sub,
        MediaUploadUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<MediaUploadUrlResponse>(access.Error);
        }

        if (!TryParsePurpose(request.Purpose, out var purpose))
        {
            return Result.Failure<MediaUploadUrlResponse>(Error.Validation("Upload purpose is not supported."));
        }

        if (purpose != MediaUploadPurpose.Cover)
        {
            return Result.Failure<MediaUploadUrlResponse>(Error.Validation("Upload purpose is not supported."));
        }

        if (!AllowedContentTypes.Contains(request.ContentType))
        {
            return Result.Failure<MediaUploadUrlResponse>(Error.Validation("Image type is not supported."));
        }

        if (request.ContentLength <= 0 || request.ContentLength > MaxCoverBytes)
        {
            return Result.Failure<MediaUploadUrlResponse>(Error.Validation("Cover image must be between 1 byte and 5 MB."));
        }

        var extension = ExtensionForContentType(request.ContentType);
        var storageKey = $"tenants/{tenantId:N}/covers/{Guid.NewGuid():N}{extension}";
        var ttl = TimeSpan.FromMinutes(15);
        var uploadUrl = await fileStorage.GetUploadUrlAsync(storageKey, request.ContentType, ttl, cancellationToken);

        return Result.Success(new MediaUploadUrlResponse(
            uploadUrl,
            storageKey,
            DateTimeOffset.UtcNow.Add(ttl)));
    }

    public async Task<Result<string>> UpdateCoverImageAsync(
        Guid tenantId,
        string auth0Sub,
        UpdateCoverImageRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<string>(access.Error);
        }

        var profile = access.Value!;
        var storageKey = request.CoverImageKey?.Trim();
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return Result.Failure<string>(Error.Validation("Cover image key is required."));
        }

        var expectedPrefix = $"tenants/{tenantId:N}/covers/";
        if (!storageKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return Result.Failure<string>(Error.Validation("Cover image key is not valid for this business."));
        }

        if (!await fileStorage.ExistsAsync(storageKey, cancellationToken))
        {
            return Result.Failure<string>(Error.Validation("Cover image upload was not found. Upload the file before saving."));
        }

        profile.CoverImageKey = storageKey;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var coverImageUrl = await fileStorage.GetDownloadUrlAsync(storageKey, cancellationToken);
        await InvalidateProfileCachesAsync(tenantId, cancellationToken);

        return Result.Success(coverImageUrl);
    }

    private async Task InvalidateProfileCachesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(CacheKeys.TenantProfile(tenantId), cancellationToken);

        var slugs = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => x.Slug)
            .ToListAsync(cancellationToken);

        foreach (var slug in slugs)
        {
            await cache.RemoveAsync(CacheKeys.LocationProfile(slug), cancellationToken);
        }
    }

    private async Task<Result<Domain.Tenancy.BusinessProfile>> ResolveAccessAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return Result.Failure<Domain.Tenancy.BusinessProfile>(Error.Forbidden("Authentication is required."));
        }

        var businessUser = await dbContext.BusinessUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Auth0Sub == auth0Sub, cancellationToken);

        if (businessUser is null || businessUser.TenantId != tenantId)
        {
            return Result.Failure<Domain.Tenancy.BusinessProfile>(Error.Forbidden("You do not have access to this business."));
        }

        var profile = await dbContext.BusinessProfiles.FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<Domain.Tenancy.BusinessProfile>(Error.NotFound("Business profile"));
        }

        return Result.Success(profile);
    }

    private static bool TryParsePurpose(string? value, out MediaUploadPurpose purpose)
    {
        purpose = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().Equals("cover", StringComparison.OrdinalIgnoreCase)
            && (purpose = MediaUploadPurpose.Cover) == MediaUploadPurpose.Cover;
    }

    private static string ExtensionForContentType(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".bin"
        };
}
