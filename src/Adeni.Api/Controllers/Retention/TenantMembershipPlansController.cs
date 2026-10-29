namespace Adeni.Api.Controllers.Retention;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Extensions;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Retention;
using Adeni.Domain.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

/// <summary>
/// Membership plan catalog (Sprint 37.2-A). Mutations need <c>business.retention</c>; every route needs the
/// <c>memberships</c> plan entitlement (enforced in the service).
/// </summary>
[ApiController]
[Route("api/v1/tenant/retention/memberships/plans")]
public sealed class TenantMembershipPlansController(
    IMembershipPlanService plans,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await plans.ListAsync(actor.TenantId, activeOnly, cancellationToken), Ok, HttpContext);
    }

    [HttpPost]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> Create([FromBody] CreateMembershipPlanRequest request, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        var result = await plans.CreateAsync(actor.TenantId, actor.Actor, request, cancellationToken);
        return ApiResults.FromResult(
            result,
            payload => Created($"/api/v1/tenant/retention/memberships/plans/{payload.Id}", payload),
            HttpContext);
    }

    [HttpGet("{id:guid}")]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await plans.GetAsync(actor.TenantId, id, cancellationToken), Ok, HttpContext);
    }

    [HttpPatch("{id:guid}")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMembershipPlanRequest request, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await plans.UpdateAsync(actor.TenantId, actor.Actor, id, request, cancellationToken), Ok, HttpContext);
    }

    [HttpPost("{id:guid}/deactivate")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await plans.DeactivateAsync(actor.TenantId, actor.Actor, id, cancellationToken), Ok, HttpContext);
    }

    private (Guid TenantId, RetentionActor Actor)? ResolveActor()
    {
        var access = PortalAccessHttpContext.Get(HttpContext);
        var sub = User.Identity?.IsAuthenticated == true ? User.FindFirstValue("sub") : null;
        if (sub is null && !auth0Options.Value.Enabled)
        {
            sub = Request.Headers[DevBusinessAuthMiddleware.DevAuth0SubHeader].FirstOrDefault();
        }

        // Tenant comes from the resolved business user (DB), never from client input alone;
        // TenantAccessMiddleware additionally requires X-Tenant-Id to match.
        return access is not null && !string.IsNullOrWhiteSpace(sub)
            ? (access.TenantId, new RetentionActor(sub, access.PermissionRole))
            : null;
    }
}
