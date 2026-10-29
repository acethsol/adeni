namespace Adeni.Api.Controllers.Retention;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Constants;
using Adeni.Api.Extensions;
using Adeni.Api.Middleware;
using Adeni.Application.Retention;
using Adeni.Domain.Identity;
using Adeni.Application.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

/// <summary>
/// Prepaid packages &amp; benefit ledger (Sprint 37.1). Mutations need <c>business.retention</c> and the
/// <c>packages</c> plan entitlement (enforced in the services); void is owner-only.
/// </summary>
[ApiController]
[Route("api/v1/tenant/retention")]
public sealed class TenantRetentionController(
    IPackageCatalogService catalog,
    ICustomerPackageService customerPackages,
    IBenefitRedemptionService redemption,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet("packages")]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> ListPackages([FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await catalog.ListAsync(actor.TenantId, activeOnly, cancellationToken), Ok, HttpContext);
    }

    [HttpPost("packages")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> CreatePackage([FromBody] CreatePackageDefinitionRequest request, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        var result = await catalog.CreateAsync(actor.TenantId, actor.Actor, request, cancellationToken);
        return ApiResults.FromResult(result, payload => Created($"/api/v1/tenant/retention/packages/{payload.Id}", payload), HttpContext);
    }

    [HttpGet("packages/{id:guid}")]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> GetPackage(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await catalog.GetAsync(actor.TenantId, id, cancellationToken), Ok, HttpContext);
    }

    [HttpPatch("packages/{id:guid}")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> UpdatePackage(Guid id, [FromBody] UpdatePackageDefinitionRequest request, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await catalog.UpdateAsync(actor.TenantId, actor.Actor, id, request, cancellationToken), Ok, HttpContext);
    }

    [HttpPost("packages/{id:guid}/deactivate")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> DeactivatePackage(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await catalog.DeactivateAsync(actor.TenantId, actor.Actor, id, cancellationToken), Ok, HttpContext);
    }

    [HttpGet("customers/{tenantCustomerId:guid}/packages")]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> ListCustomerPackages(Guid tenantCustomerId, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await customerPackages.ListForCustomerAsync(actor.TenantId, tenantCustomerId, cancellationToken), Ok, HttpContext);
    }

    [HttpPost("customers/{tenantCustomerId:guid}/packages/sell")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> SellPackage(Guid tenantCustomerId, [FromBody] SellPackageRequest request, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        if (Request.Headers.ContainsKey(IdempotencyHeaders.Key) && Request.GetIdempotencyKey() is null)
        {
            return this.InvalidIdempotencyKeyResult();
        }

        var result = await customerPackages.SellAsync(
            actor.TenantId, actor.Actor, tenantCustomerId, request, Request.GetIdempotencyKey(), cancellationToken);
        return ApiResults.FromResult(result, payload => Created($"/api/v1/tenant/retention/customer-packages/{payload.Id}", payload), HttpContext);
    }

    [HttpGet("customer-packages/{id:guid}")]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> GetCustomerPackage(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await customerPackages.GetAsync(actor.TenantId, id, cancellationToken), Ok, HttpContext);
    }

    [HttpGet("customer-packages/{id:guid}/ledger")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> GetLedger(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await customerPackages.GetLedgerAsync(actor.TenantId, id, cancellationToken), Ok, HttpContext);
    }

    [HttpPost("customer-packages/{id:guid}/void")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> VoidCustomerPackage(Guid id, [FromBody] VoidCustomerPackageRequest? request, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        if (Request.Headers.ContainsKey(IdempotencyHeaders.Key) && Request.GetIdempotencyKey() is null)
        {
            return this.InvalidIdempotencyKeyResult();
        }

        var result = await customerPackages.VoidAsync(
            actor.TenantId, actor.Actor, id, request ?? new VoidCustomerPackageRequest(), Request.GetIdempotencyKey(), cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("customers/{tenantCustomerId:guid}/redeemable-benefits")]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> RedeemableBenefits(
        Guid tenantCustomerId,
        [FromQuery] Guid? businessLocationId,
        [FromQuery] Guid[]? serviceOfferingIds,
        CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        var result = await redemption.ListRedeemableAsync(
            actor.TenantId, tenantCustomerId, businessLocationId, serviceOfferingIds, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
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