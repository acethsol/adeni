namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Booking;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/staff")]
public sealed class TenantStaffController(
    IStaffService staff,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        if (tenantId is null)
        {
            return Unauthorized();
        }

        var items = await staff.ListForTenantAsync(tenantId.Value, cancellationToken);
        return Ok(new { items });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateStaffMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await staff.CreateAsync(tenantId, request, cancellationToken);
        return ApiResults.FromResult(
            result,
            payload => Created($"/api/v1/tenant/staff/{payload.Id}", payload),
            HttpContext);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateStaffMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await staff.UpdateAsync(tenantId, id, request, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await staff.DeactivateAsync(tenantId, id, cancellationToken);
        return ApiResults.FromResult(result, () => NoContent(), HttpContext);
    }

    [HttpPut("{id:guid}/services")]
    public async Task<IActionResult> ReplaceServices(
        Guid id,
        [FromBody] ReplaceStaffServicesRequest request,
        CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await staff.ReplaceServicesAsync(
            tenantId,
            id,
            request.ServiceOfferingIds ?? Array.Empty<Guid>(),
            cancellationToken);
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

public sealed record ReplaceStaffServicesRequest(IReadOnlyList<Guid>? ServiceOfferingIds);
