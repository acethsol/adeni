namespace Adeni.Infrastructure.Tests.Storage;

using Adeni.Application.Storage;
using Adeni.Infrastructure.Storage;
using Microsoft.Extensions.Options;

public sealed class LocalFileStoragePathTests
{
    [Fact]
    public void GetFullPath_RejectsTraversalKeys()
    {
        var root = Path.Combine(Path.GetTempPath(), "adeni-local-storage-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var storage = new LocalFileStorage(
                Options.Create(new StorageOptions { Provider = "Local", Local = new LocalStorageOptions { RootPath = root } }),
                new UploadSignatureValidator(Options.Create(new StorageOptions())));

            Assert.Throws<InvalidOperationException>(() =>
                storage.SaveAsync("../escape.txt", Stream.Null, "text/plain").GetAwaiter().GetResult());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
