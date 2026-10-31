namespace Adeni.Infrastructure.Tests.Storage;

using Adeni.Application.Storage;

public sealed class FakeFileStorage : IFileStorage
{
    private readonly HashSet<string> _existingKeys = new(StringComparer.Ordinal);

    public Task<string> GetUploadUrlAsync(
        string storageKey,
        string contentType,
        TimeSpan ttl,
        CancellationToken cancellationToken = default) =>
        Task.FromResult($"http://test.local/media-upload?key={Uri.EscapeDataString(storageKey)}");

    public Task<string> GetDownloadUrlAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult($"http://test.local/media/{storageKey.TrimStart('/')}");

    public Task SaveAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        _existingKeys.Add(storageKey);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_existingKeys.Contains(storageKey));

    public Task<StoredObjectMetadata?> GetStoredMetadataAsync(
        string storageKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_existingKeys.Contains(storageKey)
            ? new StoredObjectMetadata(1024, "image/png")
            : null);

    public Task<byte[]?> ReadBytesAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<byte[]?>(_existingKeys.Contains(storageKey)
            ? new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }
            : null);

    public void SeedKey(string storageKey) => _existingKeys.Add(storageKey);

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        _existingKeys.Remove(storageKey);
        return Task.CompletedTask;
    }
}
