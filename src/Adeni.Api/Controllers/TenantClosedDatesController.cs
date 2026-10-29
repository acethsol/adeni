namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Booking;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/closed-dates")]
[RequiresBusinessPermission(BusinessPermissions.Hours)]
public sealed class TenantClosedDatesController(
    IBusinessClosedDateService closedDates,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await closedDates.ListAsync(tenantId, locationId, cancellationToken);
        return ApiResults.FromResult(result, items => Ok(new { items }), HttpContext);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateBusinessClosedDateRequest body,
        [FromQuery] Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await closedDates.CreateAsync(tenantId, body, locationId, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await closedDates.DeleteAsync(tenantId, id, cancellationToken);
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
