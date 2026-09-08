namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Trust;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Auth;
using Adeni.Infrastructure.Trust;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

public sealed record RequestVerificationBadgeApiRequest(
    string BadgeType,
    string? ReferenceNumber);

[ApiController]
[Route("api/v1/tenant/verification")]
public sealed class TenantVerificationController(
    IVerificationBadgeService badges,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet("badges")]
    public async Task<IActionResult> ListBadges(CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var items = await badges.GetGrantedBadgesAsync(tenantId, cancellationToken);
        return Ok(new { items });
    }

    [HttpPost("badges")]
    public async Task<IActionResult> RequestBadge(
        [FromBody] RequestVerificationBadgeApiRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        RequestVerificationBadgeRequest mapped;
        try
        {
            mapped = new RequestVerificationBadgeRequest(
                VerificationBadgeService.FromApiBadgeType(request.BadgeType),
                request.ReferenceNumber);
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest(new { title = "Unknown badge type." });
        }

        var result = await badges.RequestBadgeAsync(tenantId, auth0Sub, mapped, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
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

[ApiController]
[Route("api/v1/admin/verification")]
[Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy)]
public sealed class AdminVerificationController(IVerificationBadgeService badges) : ControllerBase
{
    [HttpGet("pending-badges")]
    public async Task<IActionResult> ListPendingBadges(CancellationToken cancellationToken)
    {
        var items = await badges.ListPendingRequestsAsync(cancellationToken);
        return Ok(new { items });
    }

    [HttpPost("businesses/{tenantId:guid}/badges/{badgeType}/grant")]
    public async Task<IActionResult> GrantBadge(
        Guid tenantId,
        string badgeType,
        CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await badges.GrantBadgeAsync(
            tenantId,
            VerificationBadgeService.FromApiBadgeType(badgeType),
            adminId,
            cancellationToken);

        return ApiResults.FromResult(result, _ => NoContent(), HttpContext);
    }

    [HttpPost("businesses/{tenantId:guid}/badges/{badgeType}/revoke")]
    public async Task<IActionResult> RevokeBadge(
        Guid tenantId,
        string badgeType,
        CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await badges.RevokeBadgeAsync(
            tenantId,
            VerificationBadgeService.FromApiBadgeType(badgeType),
            adminId,
            cancellationToken);

        return ApiResults.FromResult(result, _ => NoContent(), HttpContext);
    }
}
