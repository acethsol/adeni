namespace Adeni.Api.Controllers.Retention;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Constants;
using Adeni.Api.Extensions;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Retention;
using Adeni.Domain.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

/// <summary>
/// Customer memberships, in-person (Sprint 37.2-B). Every route needs the <c>memberships</c> plan entitlement
/// (enforced in the service); enrol needs <c>business.retention</c> + <c>Idempotency-Key</c>; freeze / cancel are owner-only.
/// </summary>
[ApiController]
[Route("api/v1/tenant/retention")]
public sealed class TenantCustomerMembershipsController(
    ICustomerMembershipService memberships,
    IRetentionAnalyticsService analytics,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet("customers/{tenantCustomerId:guid}/memberships")]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> ListForCustomer(Guid tenantCustomerId, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(
            await memberships.ListForCustomerAsync(actor.TenantId, tenantCustomerId, cancellationToken), Ok, HttpContext);
    }

    [HttpPost("customers/{tenantCustomerId:guid}/memberships/enrol")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> Enrol(
        Guid tenantCustomerId, [FromBody] EnrolMembershipRequest request, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        if (Request.Headers.ContainsKey(IdempotencyHeaders.Key) && Request.GetIdempotencyKey() is null)
        {
            return this.InvalidIdempotencyKeyResult();
        }

        var result = await memberships.EnrolAsync(
            actor.TenantId, actor.Actor, tenantCustomerId, request, Request.GetIdempotencyKey(), cancellationToken);
        return ApiResults.FromResult(
            result, payload => Created($"/api/v1/tenant/retention/customer-memberships/{payload.Id}", payload), HttpContext);
    }

    [HttpGet("customer-memberships/{id:guid}")]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await memberships.GetAsync(actor.TenantId, id, cancellationToken), Ok, HttpContext);
    }

    [HttpPost("customer-memberships/{id:guid}/freeze")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> Freeze(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await memberships.FreezeAsync(actor.TenantId, actor.Actor, id, cancellationToken), Ok, HttpContext);
    }

    [HttpPost("customer-memberships/{id:guid}/unfreeze")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> Unfreeze(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await memberships.UnfreezeAsync(actor.TenantId, actor.Actor, id, cancellationToken), Ok, HttpContext);
    }

    [HttpPost("customer-memberships/{id:guid}/cancel")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelMembershipRequest? request, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        var result = await memberships.CancelAsync(
            actor.TenantId, actor.Actor, id, request ?? new CancelMembershipRequest(), cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    /// <summary>In-person renewal (till / transfer) once the paid cycle has ended; Idempotency-Key required.</summary>
    [HttpPost("customer-memberships/{id:guid}/renew")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> Renew(Guid id, [FromBody] RenewMembershipRequest request, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        if (Request.Headers.ContainsKey(IdempotencyHeaders.Key) && Request.GetIdempotencyKey() is null)
        {
            return this.InvalidIdempotencyKeyResult();
        }

        var result = await memberships.RenewAsync(
            actor.TenantId, actor.Actor, id, request, Request.GetIdempotencyKey(), cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    /// <summary>TenantMembershipMrr: normalized monthly recurring revenue per currency (Retention-owned, not Adeni SaaS MRR).</summary>
    [HttpGet("memberships/mrr")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> Mrr(CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await analytics.GetMembershipMrrAsync(actor.TenantId, cancellationToken), Ok, HttpContext);
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
