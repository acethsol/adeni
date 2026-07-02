namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Middleware;
using Adeni.Application.Booking;
using Adeni.Application.Auth;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/availability")]
public sealed class TenantAvailabilityController(
    IAvailabilityService availability,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var rules = await availability.GetWeeklyRulesAsync(tenantId, cancellationToken);
        return Ok(new { items = rules });
    }

    [HttpPut]
    public async Task<IActionResult> Replace(
        [FromBody] ReplaceAvailabilityRequest body,
        CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await availability.ReplaceWeeklyRulesAsync(tenantId, body.Items, cancellationToken);
        return ApiResults.FromResult(result, rules => Ok(new { items = rules }));
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

public sealed record ReplaceAvailabilityRequest(IReadOnlyList<WeeklyAvailabilityRule> Items);
