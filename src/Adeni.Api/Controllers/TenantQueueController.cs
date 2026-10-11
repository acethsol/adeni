namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/queue")]
[RequiresPortalPermission(PortalPermissions.Bookings, PortalPermissions.BookingsSelf)]
public sealed class TenantQueueController(
    IQueueService queue,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        if (ResolveSelfStaffScopeOrDeny() is not { } scoped)
        {
            return ApiErrorResponseMapper.ToActionResult(ErrorCodes.PermissionDeniedError(), HttpContext);
        }

        var result = await queue.ListForTenantAsync(tenantId, scoped.StaffScope, cancellationToken: cancellationToken);
        return ApiResults.FromResult(result, items => Ok(new { items }), HttpContext);
    }

    [HttpPost]
    public async Task<IActionResult> Add(
        [FromBody] StaffAddQueueRequest body,
        CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        if (ResolveSelfStaffScopeOrDeny() is not { } scoped)
        {
            return ApiErrorResponseMapper.ToActionResult(ErrorCodes.PermissionDeniedError(), HttpContext);
        }

        var result = await queue.AddForTenantAsync(tenantId, body, scoped.StaffScope, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("{id:guid}/call")]
    public Task<IActionResult> Call(Guid id, CancellationToken cancellationToken) =>
        Run(id, (tenantId, scope, ct) => queue.CallAsync(tenantId, id, scope, ct), cancellationToken);

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(
        Guid id,
        [FromBody] StartQueueTicketRequest? body,
        CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        if (ResolveSelfStaffScopeOrDeny() is not { } scoped)
        {
            return ApiErrorResponseMapper.ToActionResult(ErrorCodes.PermissionDeniedError(), HttpContext);
        }

        var result = await queue.StartAsync(tenantId, id, body, scoped.StaffScope, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("{id:guid}/complete")]
    public Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken) =>
        Run(id, (tenantId, scope, ct) => queue.CompleteAsync(tenantId, id, scope, ct), cancellationToken);

    [HttpPost("{id:guid}/no-show")]
    public Task<IActionResult> NoShow(Guid id, CancellationToken cancellationToken) =>
        Run(id, (tenantId, scope, ct) => queue.MarkNoShowAsync(tenantId, id, scope, ct), cancellationToken);

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        Run(id, (tenantId, scope, ct) => queue.CancelAsync(tenantId, id, scope, ct), cancellationToken);

    private async Task<IActionResult> Run(
        Guid id,
        Func<Guid, Guid?, CancellationToken, Task<Result<QueueTicketResponse>>> action,
        CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        if (ResolveSelfStaffScopeOrDeny() is not { } scoped)
        {
            return ApiErrorResponseMapper.ToActionResult(ErrorCodes.PermissionDeniedError(), HttpContext);
        }

        var result = await action(tenantId, scoped.StaffScope, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    private SelfStaffScope? ResolveSelfStaffScopeOrDeny()
    {
        var (failClosed, staffScope) = PortalAccessHttpContext.ResolveBookingsStaffScope(
            PortalAccessHttpContext.Get(HttpContext));
        if (failClosed)
        {
            return null;
        }

        return new SelfStaffScope(staffScope);
    }

    private readonly record struct SelfStaffScope(Guid? StaffScope);

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
