namespace Adeni.Application.Storage;

using Adeni.Domain.Common;

public enum MediaUploadPurpose
{
    Cover,
    Gallery
}

public sealed record MediaUploadUrlRequest(
    string Purpose,
    string ContentType,
    long ContentLength);

public sealed record MediaUploadUrlResponse(
    string UploadUrl,
    string StorageKey,
    DateTimeOffset ExpiresAt);

public sealed record UpdateCoverImageRequest(string CoverImageKey);

public sealed record AddGalleryImageRequest(string GalleryImageKey);

public sealed record RemoveGalleryImageRequest(string GalleryImageKey);

public interface ITenantMediaService
{
    Task<Result<MediaUploadUrlResponse>> CreateUploadUrlAsync(
        Guid tenantId,
        string auth0Sub,
        MediaUploadUrlRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<string>> UpdateCoverImageAsync(
        Guid tenantId,
        string auth0Sub,
        UpdateCoverImageRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<Adeni.Application.Tenancy.GalleryImageResponse>>> AddGalleryImageAsync(
        Guid tenantId,
        string auth0Sub,
        AddGalleryImageRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<Adeni.Application.Tenancy.GalleryImageResponse>>> RemoveGalleryImageAsync(
        Guid tenantId,
        string auth0Sub,
        RemoveGalleryImageRequest request,
        CancellationToken cancellationToken = default);
}
