namespace Adeni.Api.Tests.Controllers;

using Adeni.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

public sealed class HealthControllerTests
{
    [Fact]
    public async Task Get_returns_healthy_payload_without_database()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var controller = new HealthController(services);

        var result = await controller.Get(CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }
}
