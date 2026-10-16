namespace Adeni.Api.Controllers;

using Adeni.Application.Admin;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/admin/metrics/revenue")]
[Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy)]
public sealed class AdminRevenueMetricsController(IAdminRevenueMetricsService revenueMetrics) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string granularity = "month",
        CancellationToken cancellationToken = default)
    {
        var normalized = granularity.Equals("year", StringComparison.OrdinalIgnoreCase) ? "year" : "month";
        var metrics = await revenueMetrics.GetMetricsAsync(normalized, cancellationToken);
        return Ok(metrics);
    }
}
