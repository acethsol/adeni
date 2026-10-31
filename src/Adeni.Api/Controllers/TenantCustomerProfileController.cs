namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Tenancy;
using Adeni.Application.Auth;
using Adeni.Domain.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/customers/{id:guid}")]
[RequiresBusinessPermission(BusinessPermissions.Bookings)]
public sealed class TenantCustomerProfileController(ICustomerProfileQueryService profiles, IOptions<Auth0Options> auth0) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<IActionResult> Profile(Guid id, [FromQuery] Guid? locationId, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await profiles.GetProfileAsync(actor.TenantId, id, actor.Sub, locationId, cancellationToken), Ok, HttpContext);
    }

    [HttpGet("activity")]
    [RequiresOnPremisesPresence]
    public async Task<IActionResult> Activity(Guid id, [FromQuery] Guid? locationId, [FromQuery] int pageSize = 25,
        [FromQuery] string? cursor = null, CancellationToken cancellationToken = default)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await profiles.GetActivityAsync(actor.TenantId, id, actor.Sub, locationId, pageSize, cursor, cancellationToken), Ok, HttpContext);
    }

    private (Guid TenantId, string Sub)? ResolveActor()
    {
        var access = PortalAccessHttpContext.Get(HttpContext);
        var sub = User.Identity?.IsAuthenticated == true ? User.FindFirstValue("sub") : null;
        if (sub is null && !auth0.Value.Enabled)
            sub = Request.Headers[DevBusinessAuthMiddleware.DevAuth0SubHeader].FirstOrDefault();
        return access is not null && !string.IsNullOrWhiteSpace(sub)
            && Guid.TryParse(Request.Headers[TenantAccessMiddleware.TenantHeaderName], out var tenantId)
            ? (tenantId, sub) : null;
    }
}
