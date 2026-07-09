namespace Adeni.Infrastructure.Storage;

using Adeni.Application.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddAdeniStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<UploadSignatureValidator>();

        var provider = configuration.GetSection(StorageOptions.SectionName)["Provider"] ?? "Local";
        if (string.Equals(provider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IFileStorage, AzureBlobFileStorage>();
        }
        else
        {
            services.AddSingleton<IFileStorage, LocalFileStorage>();
        }

        services.AddScoped<ITenantMediaService, TenantMediaService>();
        return services;
    }
}
