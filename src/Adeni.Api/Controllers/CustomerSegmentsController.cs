namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Tenancy;
using Adeni.Domain.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant")]
[RequiresBusinessPermission(BusinessPermissions.Bookings)]
public sealed class CustomerSegmentsController(ICustomerSegmentsQueryService segments, IOptions<Auth0Options> auth0) : ControllerBase
{
    [HttpGet("customer-segments/definitions")]
    public async Task<IActionResult> Definitions(CancellationToken ct)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await segments.DefinitionsAsync(actor.Tenant, actor.Sub, ct), Ok, HttpContext);
    }

    [HttpGet("customers/{id:guid}/segments")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] Guid? locationId, CancellationToken ct)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        Private();
        return ApiResults.FromResult(await segments.GetAsync(actor.Tenant, [id], actor.Sub, locationId, ct), rows => Ok(rows.Single()), HttpContext);
    }

    [HttpPost("customers/segments/batch")]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Batch([FromBody] CustomerSegmentsBatchRequest request, CancellationToken ct)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        Private();
        return ApiResults.FromResult(await segments.GetAsync(actor.Tenant, request.CustomerIds, actor.Sub, request.LocationId, ct), Ok, HttpContext);
    }

    private void Private() => Response.Headers.CacheControl = "private, no-store";
    private (Guid Tenant, string Sub)? ResolveActor()
    {
        var sub = User.Identity?.IsAuthenticated == true ? User.FindFirstValue("sub") : null;
        if (sub is null && !auth0.Value.Enabled) sub = Request.Headers[DevBusinessAuthMiddleware.DevAuth0SubHeader].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(sub) && Guid.TryParse(Request.Headers[TenantAccessMiddleware.TenantHeaderName], out var tenant)
            ? (tenant, sub) : null;
    }
}
