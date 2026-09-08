namespace Adeni.Infrastructure.Storage;

using Adeni.Application.Storage;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerMediaService(
    AdeniDbContext dbContext,
    IFileStorage fileStorage) : ICustomerMediaService
{
    private const long MaxQuotePhotoBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    public async Task<Result<MediaUploadUrlResponse>> CreateQuotePhotoUploadUrlAsync(
        string customerAuth0Sub,
        MediaUploadUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<MediaUploadUrlResponse>(Error.Forbidden("Customer authentication is required."));
        }

        if (!request.Purpose.Trim().Equals("quote_photo", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<MediaUploadUrlResponse>(Error.Validation("Upload purpose is not supported."));
        }

        if (!AllowedContentTypes.Contains(request.ContentType))
        {
            return Result.Failure<MediaUploadUrlResponse>(Error.Validation("Image type is not supported."));
        }

        if (request.ContentLength <= 0 || request.ContentLength > MaxQuotePhotoBytes)
        {
            return Result.Failure<MediaUploadUrlResponse>(Error.Validation("Photo must be between 1 byte and 5 MB."));
        }

        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Auth0Sub == customerAuth0Sub, cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                Auth0Sub = customerAuth0Sub,
                Name = string.Empty,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            dbContext.Customers.Add(customer);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var extension = ExtensionForContentType(request.ContentType);
        var storageKey = $"customers/{customer.Id:N}/quote-photos/{Guid.NewGuid():N}{extension}";
        var ttl = TimeSpan.FromMinutes(15);
        var uploadUrl = await fileStorage.GetUploadUrlAsync(storageKey, request.ContentType, ttl, cancellationToken);

        return Result.Success(new MediaUploadUrlResponse(
            uploadUrl,
            storageKey,
            DateTimeOffset.UtcNow.Add(ttl)));
    }

    private static string ExtensionForContentType(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".bin",
        };
}
