namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Booking;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/kiosk")]
[RequiresBusinessPermission(BusinessPermissions.Bookings, BusinessPermissions.BookingsSelf)]
public sealed class TenantKioskController(
    IKioskService kiosk,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await kiosk.GetStatusAsync(tenantId, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPut("pin")]
    [RequiresBusinessPermission(BusinessPermissions.Bookings)]
    public async Task<IActionResult> SetPin(
        [FromBody] SetKioskPinRequest body,
        CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await kiosk.SetPinAsync(tenantId, body, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("verify-pin")]
    public async Task<IActionResult> VerifyPin(
        [FromBody] VerifyKioskPinRequest body,
        CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await kiosk.VerifyPinAsync(tenantId, body, cancellationToken);
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
