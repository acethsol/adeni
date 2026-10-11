namespace Adeni.Infrastructure.Storage;

using Adeni.Application.Caching;
using Adeni.Application.Storage;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class TenantMediaService(
    AdeniDbContext dbContext,
    IFileStorage fileStorage,
    ICacheService cache) : ITenantMediaService
{
    public const int MaxGalleryImages = GalleryImageKeys.MaxCount;
    private const long MaxImageBytes = 5 * 1024 * 1024;

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

        if (!AllowedContentTypes.Contains(request.ContentType))
        {
            return Result.Failure<MediaUploadUrlResponse>(Error.Validation("Image type is not supported."));
        }

        if (request.ContentLength <= 0 || request.ContentLength > MaxImageBytes)
        {
            return Result.Failure<MediaUploadUrlResponse>(Error.Validation("Image must be between 1 byte and 5 MB."));
        }

        var extension = ExtensionForContentType(request.ContentType);
        var folder = purpose switch
        {
            MediaUploadPurpose.Gallery => "gallery",
            MediaUploadPurpose.StaffAvatar => "staff",
            _ => "covers",
        };
        var storageKey = $"tenants/{tenantId:N}/{folder}/{Guid.NewGuid():N}{extension}";
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

    public async Task<Result<IReadOnlyList<GalleryImageResponse>>> AddGalleryImageAsync(
        Guid tenantId,
        string auth0Sub,
        AddGalleryImageRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GalleryImageResponse>>(access.Error);
        }

        var profile = access.Value!;
        var storageKey = request.GalleryImageKey?.Trim();
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return Result.Failure<IReadOnlyList<GalleryImageResponse>>(Error.Validation("Gallery image key is required."));
        }

        var expectedPrefix = $"tenants/{tenantId:N}/gallery/";
        if (!storageKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return Result.Failure<IReadOnlyList<GalleryImageResponse>>(
                Error.Validation("Gallery image key is not valid for this business."));
        }

        if (!await fileStorage.ExistsAsync(storageKey, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<GalleryImageResponse>>(
                Error.Validation("Gallery image upload was not found. Upload the file before saving."));
        }

        var keys = GalleryImageKeys.Deserialize(profile.GalleryImageKeysJson);
        if (keys.Contains(storageKey, StringComparer.Ordinal))
        {
            return Result.Success(await ResolveGalleryAsync(keys, cancellationToken));
        }

        if (keys.Count >= MaxGalleryImages)
        {
            return Result.Failure<IReadOnlyList<GalleryImageResponse>>(
                Error.Validation($"You can add up to {MaxGalleryImages} gallery photos."));
        }

        keys.Add(storageKey);
        profile.GalleryImageKeysJson = GalleryImageKeys.Serialize(keys);
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateProfileCachesAsync(tenantId, cancellationToken);

        return Result.Success(await ResolveGalleryAsync(keys, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<GalleryImageResponse>>> RemoveGalleryImageAsync(
        Guid tenantId,
        string auth0Sub,
        RemoveGalleryImageRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<IReadOnlyList<GalleryImageResponse>>(access.Error);
        }

        var profile = access.Value!;
        var storageKey = request.GalleryImageKey?.Trim();
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return Result.Failure<IReadOnlyList<GalleryImageResponse>>(Error.Validation("Gallery image key is required."));
        }

        var keys = GalleryImageKeys.Deserialize(profile.GalleryImageKeysJson);
        if (!keys.Remove(storageKey))
        {
            return Result.Failure<IReadOnlyList<GalleryImageResponse>>(Error.NotFound("Gallery image"));
        }

        profile.GalleryImageKeysJson = GalleryImageKeys.Serialize(keys);
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateProfileCachesAsync(tenantId, cancellationToken);

        return Result.Success(await ResolveGalleryAsync(keys, cancellationToken));
    }

    private async Task<IReadOnlyList<GalleryImageResponse>> ResolveGalleryAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
    {
        var items = new List<GalleryImageResponse>(keys.Count);
        foreach (var key in keys)
        {
            var url = await fileStorage.GetDownloadUrlAsync(key, cancellationToken);
            items.Add(new GalleryImageResponse(key, url));
        }

        return items;
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

    private async Task<Result<BusinessProfile>> ResolveAccessAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return Result.Failure<BusinessProfile>(Error.Forbidden("Authentication is required."));
        }

        var businessUser = await dbContext.BusinessUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Auth0Sub == auth0Sub, cancellationToken);

        if (businessUser is null || businessUser.TenantId != tenantId)
        {
            return Result.Failure<BusinessProfile>(Error.Forbidden("You do not have access to this business."));
        }

        var profile = await dbContext.BusinessProfiles.FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<BusinessProfile>(Error.NotFound("Business profile"));
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

        if (value.Trim().Equals("cover", StringComparison.OrdinalIgnoreCase))
        {
            purpose = MediaUploadPurpose.Cover;
            return true;
        }

        if (value.Trim().Equals("gallery", StringComparison.OrdinalIgnoreCase))
        {
            purpose = MediaUploadPurpose.Gallery;
            return true;
        }

        if (value.Trim().Equals("staff_avatar", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("staffAvatar", StringComparison.OrdinalIgnoreCase))
        {
            purpose = MediaUploadPurpose.StaffAvatar;
            return true;
        }

        return false;
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
