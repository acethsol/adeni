namespace Adeni.Application.Storage;

using Adeni.Domain.Common;

public interface ICustomerMediaService
{
    Task<Result<MediaUploadUrlResponse>> CreateQuotePhotoUploadUrlAsync(
        string customerAuth0Sub,
        MediaUploadUrlRequest request,
        CancellationToken cancellationToken = default);
}
