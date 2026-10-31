namespace Adeni.Application.Storage;

using Adeni.Domain.Common;

public interface IMediaUploadBlobValidator
{
    Task<Result> ValidateConfirmedUploadAsync(
        Guid tenantId,
        MediaUploadPurpose purpose,
        string storageKey,
        CancellationToken cancellationToken = default);
}
