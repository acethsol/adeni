namespace Adeni.Infrastructure.Storage;

using Adeni.Application.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

public sealed class AzureBlobFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly AzureBlobStorageOptions _options = options.Value.AzureBlob;

    public Task<string> GetUploadUrlAsync(
        string storageKey,
        string contentType,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var client = GetBlobClient(storageKey);
        if (!client.CanGenerateSasUri)
        {
            throw new InvalidOperationException("Azure Blob client cannot generate SAS URIs.");
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _options.PublicContainer,
            BlobName = storageKey,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(ttl),
            ContentType = contentType
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);

        return Task.FromResult(client.GenerateSasUri(sasBuilder).ToString());
    }

    public Task<string> GetDownloadUrlAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            return Task.FromResult($"{_options.PublicBaseUrl!.TrimEnd('/')}/{storageKey.TrimStart('/')}");
        }

        return Task.FromResult(GetBlobClient(storageKey).Uri.ToString());
    }

    public async Task SaveAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var client = GetBlobClient(storageKey);
        await client.UploadAsync(content, overwrite: true, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var response = await GetBlobClient(storageKey).ExistsAsync(cancellationToken);
        return response.Value;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        await GetBlobClient(storageKey).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private BlobClient GetBlobClient(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException("Azure Blob connection string is not configured.");
        }

        var serviceClient = new BlobServiceClient(_options.ConnectionString);
        var containerClient = serviceClient.GetBlobContainerClient(_options.PublicContainer);
        return containerClient.GetBlobClient(storageKey);
    }
}
