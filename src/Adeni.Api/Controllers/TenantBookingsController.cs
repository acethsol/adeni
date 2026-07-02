namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Middleware;
using Adeni.Application.Booking;
using Adeni.Application.Auth;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/bookings")]
public sealed class TenantBookingsController(
    IBookingService bookings,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var items = await bookings.ListForTenantAsync(tenantId, cancellationToken);
        return Ok(new { items });
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await bookings.AcceptAsync(tenantId, id, cancellationToken);
        return ApiResults.FromResult(result, Ok);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectBookingRequest body,
        CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await bookings.RejectAsync(tenantId, id, body.Reason, cancellationToken);
        return ApiResults.FromResult(result, Ok);
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

public sealed record RejectBookingRequest(string? Reason);
