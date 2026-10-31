namespace Adeni.Infrastructure.Tests.Storage;

using Adeni.Application.Storage;
using Adeni.Infrastructure.Storage;

public sealed class MediaUploadBlobValidatorTests
{
    [Fact]
    public async Task ValidateConfirmedUpload_RejectsOversizedImage()
    {
        var tenantId = Guid.NewGuid();
        var key = $"tenants/{tenantId:N}/covers/photo.jpg";
        var storage = new FakeFileStorage(new StoredObjectMetadata(6 * 1024 * 1024, "image/jpeg"), [0xFF, 0xD8, 0xFF, 0xDB]);
        var validator = new MediaUploadBlobValidator(storage);

        var result = await validator.ValidateConfirmedUploadAsync(
            tenantId,
            MediaUploadPurpose.Cover,
            key);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ValidateConfirmedUpload_AcceptsValidPng()
    {
        var tenantId = Guid.NewGuid();
        var key = $"tenants/{tenantId:N}/gallery/photo.png";
        var storage = new FakeFileStorage(
            new StoredObjectMetadata(1024, "image/png"),
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var validator = new MediaUploadBlobValidator(storage);

        var result = await validator.ValidateConfirmedUploadAsync(
            tenantId,
            MediaUploadPurpose.Gallery,
            key);

        Assert.True(result.IsSuccess);
    }

    private sealed class FakeFileStorage(StoredObjectMetadata? metadata, byte[] bytes) : IFileStorage
    {
        public Task<string> GetUploadUrlAsync(string storageKey, string contentType, TimeSpan ttl, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<string> GetDownloadUrlAsync(string storageKey, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task SaveAsync(string storageKey, Stream content, string contentType, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(metadata is not null);

        public Task<StoredObjectMetadata?> GetStoredMetadataAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(metadata);

        public Task<byte[]?> ReadBytesAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]?>(bytes);

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
