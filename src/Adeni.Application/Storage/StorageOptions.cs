namespace Adeni.Application.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "Local";

    public LocalStorageOptions Local { get; set; } = new();

    public AzureBlobStorageOptions AzureBlob { get; set; } = new();
}

public sealed class LocalStorageOptions
{
    public string RootPath { get; set; } = "./.data/media";

    public string PublicBaseUrl { get; set; } = "http://localhost:5169/media";

    public string ApiBaseUrl { get; set; } = "http://localhost:5169";

    public string UploadSigningKey { get; set; } = "dev-upload-signing-key";
}

public sealed class AzureBlobStorageOptions
{
    public string? ConnectionString { get; set; }

    public string PublicContainer { get; set; } = "media";

    public string? PublicBaseUrl { get; set; }
}
