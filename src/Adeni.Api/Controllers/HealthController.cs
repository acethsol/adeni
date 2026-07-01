namespace Adeni.Api.Controllers;

using Adeni.Application.Caching;
using Adeni.Infrastructure.Auth;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("health")]
public sealed class HealthController(
    IServiceProvider services,
    IOptions<RedisOptions> redisOptions,
    IRedisHealthCheck redisHealthCheck) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var checks = new Dictionary<string, string> { ["api"] = "healthy" };
        var dbContext = services.GetService<AdeniDbContext>();

        if (dbContext is null)
        {
            checks["database"] = "not_configured";
        }
        else
        {
            try
            {
                checks["database"] = await dbContext.Database.CanConnectAsync(cancellationToken)
                    ? "healthy"
                    : "unhealthy";
            }
            catch (Exception)
            {
                checks["database"] = "unhealthy";
            }
        }

        if (!redisOptions.Value.Enabled)
        {
            checks["cache"] = "memory_fallback";
        }
        else
        {
            try
            {
                checks["cache"] = await redisHealthCheck.PingAsync(cancellationToken)
                    ? "healthy"
                    : "unhealthy";
            }
            catch (Exception)
            {
                checks["cache"] = "unhealthy";
            }
        }

        var status = checks.Values.All(v => v is "healthy" or "not_configured" or "memory_fallback")
            ? "healthy"
            : "degraded";
        return Ok(new { status, service = "adeni-api", checks });
    }
}

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy)]
public sealed class AdminDiagnosticsController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { message = "admin ok" });
}
