namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Common;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Booking;
using TenantBookingTabs = Adeni.Application.Booking.TenantBookingTabs;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/bookings")]
[RequiresPortalPermission(PortalPermissions.Bookings, PortalPermissions.BookingsSelf)]
public sealed class TenantBookingsController(
    IBookingService bookings,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string tab = TenantBookingTabs.Pending,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        [FromQuery] Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var sort = ListSortQuery.ResolveOptional(
            sortBy,
            sortDir,
            TenantBookingListSort.Allowed,
            TenantBookingListSort.DefaultField,
            TenantBookingListSort.DefaultDirection);
        if (sort.IsFailure)
        {
            return BadRequest(new { title = sort.Error!.Message, code = sort.Error.Code });
        }

        var (failClosed, staffScope) = PortalAccessHttpContext.ResolveBookingsStaffScope(
            PortalAccessHttpContext.Get(HttpContext));
        if (failClosed)
        {
            return Ok(new { items = Array.Empty<object>(), page = 1, pageSize, totalCount = 0 });
        }

        if (!TenantBookingTabs.IsValid(tab) || tab == TenantBookingTabs.All)
        {
            tab = TenantBookingTabs.Pending;
        }

        var result = await bookings.ListForTenantAsync(
            tenantId,
            tab,
            page,
            pageSize,
            staffScope,
            locationId,
            sort.Value!.SortBy,
            sort.Value.SortDirection,
            cancellationToken);

        return Ok(new
        {
            items = result.Items,
            page = result.Page,
            pageSize = result.PageSize,
            totalCount = result.TotalCount,
        });
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        if (ResolveSelfStaffScopeOrDeny() is not { } scoped)
        {
            return ApiErrorResponseMapper.ToActionResult(
                ErrorCodes.PermissionDeniedError(),
                HttpContext);
        }

        var result = await bookings.AcceptAsync(tenantId, id, scoped.StaffScope, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
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

        if (ResolveSelfStaffScopeOrDeny() is not { } scoped)
        {
            return ApiErrorResponseMapper.ToActionResult(
                ErrorCodes.PermissionDeniedError(),
                HttpContext);
        }

        var result = await bookings.RejectAsync(
            tenantId,
            id,
            body.Reason,
            scoped.StaffScope,
            cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("{id:guid}/check-in")]
    public async Task<IActionResult> CheckIn(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        if (ResolveSelfStaffScopeOrDeny() is not { } scoped)
        {
            return ApiErrorResponseMapper.ToActionResult(
                ErrorCodes.PermissionDeniedError(),
                HttpContext);
        }

        var result = await bookings.CheckInAsync(tenantId, id, scoped.StaffScope, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        if (ResolveSelfStaffScopeOrDeny() is not { } scoped)
        {
            return ApiErrorResponseMapper.ToActionResult(
                ErrorCodes.PermissionDeniedError(),
                HttpContext);
        }

        var result = await bookings.CompleteAsync(tenantId, id, scoped.StaffScope, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("{id:guid}/mark-paid")]
    public async Task<IActionResult> MarkPaid(
        Guid id,
        [FromBody] MarkBookingPaidRequest? body,
        CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        if (ResolveSelfStaffScopeOrDeny() is not { } scoped)
        {
            return ApiErrorResponseMapper.ToActionResult(
                ErrorCodes.PermissionDeniedError(),
                HttpContext);
        }

        var result = await bookings.MarkPaidAsync(
            tenantId,
            id,
            body ?? new MarkBookingPaidRequest(),
            scoped.StaffScope,
            cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("{id:guid}/collect-balance")]
    public async Task<IActionResult> CollectBalance(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        if (ResolveSelfStaffScopeOrDeny() is not { } scoped)
        {
            return ApiErrorResponseMapper.ToActionResult(
                ErrorCodes.PermissionDeniedError(),
                HttpContext);
        }

        var result = await bookings.CollectBalanceAsync(tenantId, id, scoped.StaffScope, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    /// <summary>
    /// Null means deny (self-only without linked staff). Otherwise staff scope to enforce
    /// (null StaffScope = full bookings access).
    /// </summary>
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

public sealed record RejectBookingRequest(string? Reason);
