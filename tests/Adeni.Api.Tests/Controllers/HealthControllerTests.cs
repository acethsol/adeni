namespace Adeni.Api.Tests.Controllers;

using Adeni.Api.Controllers;
using Adeni.Application.Caching;
using Adeni.Infrastructure.Caching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public sealed class HealthControllerTests
{
    [Fact]
    public async Task Get_returns_healthy_payload_without_database()
    {
        var services = new ServiceCollection()
            .AddOptions<RedisOptions>()
            .Services
            .BuildServiceProvider();

        var controller = new HealthController(
            services,
            services.GetRequiredService<IOptions<RedisOptions>>(),
            new UnconfiguredRedisHealthCheck());

        var result = await controller.Get(CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }
}
