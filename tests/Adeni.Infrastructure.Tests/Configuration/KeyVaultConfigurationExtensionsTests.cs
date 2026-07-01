namespace Adeni.Infrastructure.Tests.Configuration;

using Adeni.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;

public sealed class KeyVaultConfigurationExtensionsTests
{
    [Fact]
    public void AddAdeniKeyVault_skips_in_development()
    {
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(new Dictionary<string, string?>());

        var result = builder.AddAdeniKeyVault(new TestHostEnvironment { EnvironmentName = Environments.Development });

        Assert.Same(builder, result);
    }

    [Fact]
    public void AddAdeniKeyVault_throws_when_uri_missing_in_production()
    {
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(new Dictionary<string, string?>());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddAdeniKeyVault(new TestHostEnvironment { EnvironmentName = Environments.Production }));

        Assert.Contains(KeyVaultConfigurationExtensions.KeyVaultUriKey, ex.Message);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Adeni.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
