namespace Adeni.Api.Controllers;

using Adeni.Application.Reporting;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/admin/reporting")]
[Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy)]
public sealed class AdminReportingController(IAdminReportingQueryService reporting) : ControllerBase
{
    [HttpGet("platform-subscription-mrr")]
    public async Task<IActionResult> PlatformSubscriptionMrr(CancellationToken cancellationToken)
    {
        var payload = await reporting.GetPlatformSubscriptionMrrAsync(cancellationToken);
        return Ok(payload);
    }

    [HttpGet("gmv")]
    public async Task<IActionResult> Gmv(
        [FromQuery] string granularity = "month",
        CancellationToken cancellationToken = default)
    {
        var payload = await reporting.GetGmvAsync(granularity, cancellationToken);
        return Ok(payload);
    }
}
