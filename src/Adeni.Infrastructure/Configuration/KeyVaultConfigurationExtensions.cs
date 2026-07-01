namespace Adeni.Infrastructure.Configuration;

using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

public static class KeyVaultConfigurationExtensions
{
    public const string KeyVaultUriKey = "KeyVault:Uri";

    public static IConfigurationBuilder AddAdeniKeyVault(
        this IConfigurationBuilder configuration,
        IHostEnvironment environment)
    {
        if (environment.IsDevelopment() || environment.EnvironmentName == "Testing")
        {
            return configuration;
        }

        var built = configuration.Build();
        var vaultUri = built[KeyVaultUriKey];

        if (string.IsNullOrWhiteSpace(vaultUri))
        {
            throw new InvalidOperationException(
                $"Configuration key '{KeyVaultUriKey}' is required in non-development environments.");
        }

        configuration.AddAzureKeyVault(new Uri(vaultUri), new DefaultAzureCredential());
        return configuration;
    }
}
