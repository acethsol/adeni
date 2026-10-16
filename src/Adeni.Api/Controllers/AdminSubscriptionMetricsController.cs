namespace Adeni.Api.Controllers;

using Adeni.Application.Subscriptions;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/admin/metrics/subscription")]
[Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy)]
public sealed class AdminSubscriptionMetricsController(
    ISubscriptionMetricsService metricsService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var metrics = await metricsService.GetAdminMetricsAsync(cancellationToken);
        return Ok(metrics);
    }
}
