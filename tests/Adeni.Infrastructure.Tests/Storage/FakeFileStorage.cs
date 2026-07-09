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

    public void SeedKey(string storageKey) => _existingKeys.Add(storageKey);

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        _existingKeys.Remove(storageKey);
        return Task.CompletedTask;
    }
}
