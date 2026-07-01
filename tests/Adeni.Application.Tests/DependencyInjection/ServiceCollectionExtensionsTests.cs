namespace Adeni.Application.Tests.DependencyInjection;

using Adeni.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddApplication_registers_without_error()
    {
        var services = new ServiceCollection();

        var result = services.AddApplication();

        Assert.Same(services, result);
    }
}
