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
        if (IsAbsoluteUrl(storageKey))
        {
            return Task.FromResult(storageKey);
        }

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

    public Task<StoredObjectMetadata?> GetStoredMetadataAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = GetFullPath(storageKey);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<StoredObjectMetadata?>(null);
        }

        var info = new FileInfo(fullPath);
        return Task.FromResult<StoredObjectMetadata?>(
            new StoredObjectMetadata(info.Length, GuessContentType(fullPath)));
    }

    public Task<byte[]?> ReadBytesAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (IsAbsoluteUrl(storageKey))
        {
            return Task.FromResult<byte[]?>(null);
        }

        var fullPath = GetFullPath(storageKey);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<byte[]?>(null);
        }

        return Task.FromResult<byte[]?>(File.ReadAllBytes(fullPath));
    }

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

    private static string? GuessContentType(string fullPath) =>
        Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => null,
        };

    private static bool IsAbsoluteUrl(string storageKey) =>
        storageKey.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || storageKey.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

    private string GetFullPath(string storageKey)
    {
        var normalizedKey = storageKey.Replace('\\', '/').TrimStart('/');
        if (normalizedKey.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid storage key path.");
        }

        var root = Path.GetFullPath(_options.RootPath);
        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        var fullPath = Path.GetFullPath(Path.Combine(root, normalizedKey.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException("Invalid storage key path.");
        }

        return fullPath;
    }
}
