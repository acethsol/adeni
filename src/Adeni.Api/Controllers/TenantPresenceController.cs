namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Identity;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/tenant/presence")]
public sealed class TenantPresenceController(IVerifiedPresenceService presence) : ControllerBase
{
    [HttpGet("me")]
    [RequiresBusinessPermission(BusinessPermissions.Overview)]
    public async Task<IActionResult> GetMySession(CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var access = PortalAccessHttpContext.Get(HttpContext);
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await presence.GetMyActiveSessionAsync(tenantId, access, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("verify")]
    [RequiresBusinessPermission(BusinessPermissions.AttendanceSelf)]
    public async Task<IActionResult> Verify(
        [FromBody] VerifyPresenceRequest request,
        CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var access = PortalAccessHttpContext.Get(HttpContext);
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await presence.VerifyAsync(tenantId, access, request, cancellationToken);
        return ApiResults.FromResult(
            result,
            payload => Created($"/api/v1/tenant/presence/me", payload),
            HttpContext);
    }

    [HttpPost("invalidate")]
    [RequiresBusinessPermission(BusinessPermissions.AttendanceSelf)]
    public async Task<IActionResult> Invalidate(CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var access = PortalAccessHttpContext.Get(HttpContext);
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await presence.InvalidateMySessionAsync(tenantId, access, cancellationToken);
        return ApiResults.FromResult(result, () => NoContent(), HttpContext);
    }

    private Guid? ResolveTenantId()
    {
        var tenantClaim = User.FindFirstValue(AdeniClaimTypes.TenantId);
        if (Guid.TryParse(tenantClaim, out var tenantId))
        {
            return tenantId;
        }

        if (Request.Headers.TryGetValue(TenantAccessMiddleware.TenantHeaderName, out var headerValue)
            && Guid.TryParse(headerValue, out tenantId))
        {
            return tenantId;
        }

        return null;
    }
}
