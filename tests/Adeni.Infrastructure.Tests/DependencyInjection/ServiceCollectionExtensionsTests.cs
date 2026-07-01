namespace Adeni.Infrastructure.Tests.DependencyInjection;

using Adeni.Application.Abstractions;
using Adeni.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class InfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfrastructure_registers_core_services()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ICorrelationContext>());
        Assert.NotNull(provider.GetService<IAuditLogWriter>());
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
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<Adeni.Infrastructure.Persistence.AdeniDbContext>());
    }
}
