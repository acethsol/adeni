namespace Adeni.Api.Controllers.Retention;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Constants;
using Adeni.Api.Extensions;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Retention;
using Adeni.Domain.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/retention/analytics")]
public sealed class TenantRetentionAnalyticsController(
    IRetentionAnalyticsService analytics,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet("summary")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> Summary(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] Guid? locationId,
        CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor)
        {
            return Unauthorized();
        }

        var result = await analytics.GetSummaryAsync(
            actor.TenantId,
            new RetentionAnalyticsQuery(from, to, locationId),
            cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    private (Guid TenantId, RetentionActor Actor)? ResolveActor()
    {
        var access = PortalAccessHttpContext.Get(HttpContext);
        var sub = User.Identity?.IsAuthenticated == true ? User.FindFirstValue("sub") : null;
        if (sub is null && !auth0Options.Value.Enabled)
        {
            sub = Request.Headers[DevBusinessAuthMiddleware.DevAuth0SubHeader].FirstOrDefault();
        }

        return access is not null && !string.IsNullOrWhiteSpace(sub)
            ? (access.TenantId, new RetentionActor(sub, access.PermissionRole))
            : null;
    }
}
