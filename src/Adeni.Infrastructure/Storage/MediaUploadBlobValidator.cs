namespace Adeni.Infrastructure.Storage;

using Adeni.Application.Storage;
using Adeni.Domain.Common;

public sealed class MediaUploadBlobValidator(IFileStorage fileStorage) : IMediaUploadBlobValidator
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const long MaxVerificationDocumentBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    private static readonly HashSet<string> AllowedVerificationContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/pdf",
    };

    public async Task<Result> ValidateConfirmedUploadAsync(
        Guid tenantId,
        MediaUploadPurpose purpose,
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var key = storageKey.Trim();
        var prefix = ExpectedPrefix(tenantId, purpose);
        if (!key.StartsWith(prefix, StringComparison.Ordinal))
        {
            return Result.Failure(Error.Validation("Storage key is not valid for this upload purpose."));
        }

        var metadata = await fileStorage.GetStoredMetadataAsync(key, cancellationToken);
        if (metadata is null)
        {
            return Result.Failure(Error.Validation("Upload was not found. Upload the file before saving."));
        }

        var maxBytes = purpose == MediaUploadPurpose.VerificationDocument
            ? MaxVerificationDocumentBytes
            : MaxImageBytes;
        if (metadata.ContentLength <= 0 || metadata.ContentLength > maxBytes)
        {
            return Result.Failure(Error.Validation(
                purpose == MediaUploadPurpose.VerificationDocument
                    ? "Verification file must be between 1 byte and 10 MB."
                    : "Image must be between 1 byte and 5 MB."));
        }

        var contentType = metadata.ContentType?.Trim();
        if (string.IsNullOrWhiteSpace(contentType)
            || !IsAllowedContentType(purpose, contentType))
        {
            await TryDeleteInvalidAsync(key, cancellationToken);
            return Result.Failure(Error.Validation("Uploaded file type is not supported."));
        }

        var sniff = await fileStorage.ReadBytesAsync(key, cancellationToken);
        if (sniff is null || !MatchesMagicBytes(purpose, contentType, sniff))
        {
            await TryDeleteInvalidAsync(key, cancellationToken);
            return Result.Failure(Error.Validation("Uploaded file content does not match the declared type."));
        }

        return Result.Success();
    }

    private async Task TryDeleteInvalidAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            await fileStorage.DeleteAsync(storageKey, cancellationToken);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static string ExpectedPrefix(Guid tenantId, MediaUploadPurpose purpose)
    {
        var folder = purpose switch
        {
            MediaUploadPurpose.Gallery => "gallery",
            MediaUploadPurpose.StaffAvatar => "staff",
            MediaUploadPurpose.MessageAttachment => "messages",
            MediaUploadPurpose.VerificationDocument => "verification",
            _ => "covers",
        };
        return $"tenants/{tenantId:N}/{folder}/";
    }

    private static bool IsAllowedContentType(MediaUploadPurpose purpose, string contentType) =>
        purpose == MediaUploadPurpose.VerificationDocument
            ? AllowedVerificationContentTypes.Contains(contentType)
            : AllowedImageContentTypes.Contains(contentType);

    private static bool MatchesMagicBytes(MediaUploadPurpose purpose, string contentType, byte[] bytes)
    {
        if (bytes.Length < 4)
        {
            return false;
        }

        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 5
                && bytes[0] == 0x25
                && bytes[1] == 0x50
                && bytes[2] == 0x44
                && bytes[3] == 0x46;
        }

        if (contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
        {
            return bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
        }

        if (contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return bytes[0] == 0xFF && bytes[1] == 0xD8;
        }

        if (contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 12
                && bytes[0] == 0x52
                && bytes[1] == 0x49
                && bytes[2] == 0x46
                && bytes[3] == 0x46
                && bytes[8] == 0x57
                && bytes[9] == 0x45
                && bytes[10] == 0x42
                && bytes[11] == 0x50;
        }

        return purpose != MediaUploadPurpose.VerificationDocument;
    }
}
