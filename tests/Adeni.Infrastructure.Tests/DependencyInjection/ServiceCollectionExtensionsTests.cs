namespace Adeni.Infrastructure.Tests.DependencyInjection;

using Adeni.Application.Abstractions;
using Adeni.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

public sealed class InfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfrastructure_registers_core_services()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration, new TestHostEnvironment());

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ICorrelationContext>());
        Assert.NotNull(provider.GetService<IAuditLogWriter>());
        Assert.NotNull(provider.GetService<Application.Auth.IAuthSyncService>());
    }

    [Fact]
    public void AddInfrastructure_registers_dbcontext_when_connection_configured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AdeniDb"] = "Host=localhost;Database=test"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration, new TestHostEnvironment());

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<Adeni.Infrastructure.Persistence.AdeniDbContext>());
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Adeni.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
