namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/access")]
[RequiresPortalPermission(PortalPermissions.Staff)]
public sealed class TenantAccessController(
    IStaffAccessService staffAccess,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet("invites")]
    public async Task<IActionResult> ListInvites(CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var items = await staffAccess.ListInvitesAsync(tenantId, cancellationToken);
        return Ok(new { items });
    }

    [HttpPost("invites")]
    public async Task<IActionResult> InviteAccessOnly(
        [FromBody] CreateStaffInviteRequest request,
        CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is not { } auth0Sub || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await staffAccess.InviteAccessOnlyAsync(
            tenantId,
            request,
            auth0Sub,
            cancellationToken);

        return ApiResults.FromResult(
            result,
            payload => Created($"/api/v1/tenant/access/invites/{payload.InviteId}", payload),
            HttpContext);
    }

    [HttpPost("invites/{id:guid}/resend")]
    public async Task<IActionResult> Resend(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await staffAccess.ResendAsync(tenantId, id, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpDelete("invites/{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await staffAccess.RevokeAsync(tenantId, id, cancellationToken);
        return ApiResults.FromResult(result, () => NoContent(), HttpContext);
    }

    private string? ResolveAuth0Sub()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return User.FindFirst("sub")?.Value;
        }

        if (!auth0Options.Value.Enabled
            && Request.Headers.TryGetValue(DevBusinessAuthMiddleware.DevAuth0SubHeader, out var devSub)
            && !string.IsNullOrWhiteSpace(devSub))
        {
            return devSub.ToString();
        }

        return null;
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
