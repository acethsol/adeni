namespace Adeni.Infrastructure.Storage;

using System.Security.Cryptography;
using System.Text;
using Adeni.Application.Storage;
using Microsoft.Extensions.Options;

public sealed class LocalFileStorage(
    IOptions<StorageOptions> options,
    UploadSignatureValidator signatureValidator) : IFileStorage
{
    private readonly LocalStorageOptions _options = options.Value.Local;

    public Task<string> GetUploadUrlAsync(
        string storageKey,
        string contentType,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
        var signature = signatureValidator.Sign(storageKey, expiresAt.ToUnixTimeSeconds());
        var uploadUrl =
            $"{_options.ApiBaseUrl.TrimEnd('/')}/media-upload" +
            $"?key={Uri.EscapeDataString(storageKey)}" +
            $"&expires={expiresAt.ToUnixTimeSeconds()}" +
            $"&sig={Uri.EscapeDataString(signature)}";

        return Task.FromResult(uploadUrl);
    }

    public Task<string> GetDownloadUrlAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var url = $"{_options.PublicBaseUrl.TrimEnd('/')}/{storageKey.TrimStart('/')}";
        return Task.FromResult(url);
    }

    public async Task SaveAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(storageKey);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(GetFullPath(storageKey)));

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(storageKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public string GetRootPath() => Path.GetFullPath(_options.RootPath);

    private string GetFullPath(string storageKey)
    {
        var normalizedKey = storageKey.Replace('\\', '/').TrimStart('/');
        var root = Path.GetFullPath(_options.RootPath);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalizedKey.Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage key path.");
        }

        return fullPath;
    }
}
