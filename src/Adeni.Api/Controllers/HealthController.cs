namespace Adeni.Api.Controllers;

using Adeni.Infrastructure.Auth;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("health")]
public sealed class HealthController(IServiceProvider services) : ControllerBase
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

        var status = checks.Values.All(v => v is "healthy" or "not_configured") ? "healthy" : "degraded";
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
