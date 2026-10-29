namespace Adeni.Api.Controllers;

using Adeni.Api.Auth;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Dashboard;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/tenant/dashboard")]
[RequiresBusinessPermission(BusinessPermissions.Overview)]
public sealed class TenantDashboardController(ITenantDashboardService dashboard) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var (rangeFrom, rangeTo) = NormalizeRange(from, to);
        var (failClosed, staffScope) = PortalAccessHttpContext.ResolveBookingsStaffScope(
            PortalAccessHttpContext.Get(HttpContext));
        if (failClosed)
        {
            return Ok(new TenantDashboardSummaryResponse(
                rangeFrom,
                rangeTo,
                0,
                0,
                null,
                null,
                null,
                false));
        }

        var result = await dashboard.GetSummaryAsync(
            tenantId,
            rangeFrom,
            rangeTo,
            staffScope,
            locationId,
            cancellationToken);

        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("series")]
    public async Task<IActionResult> Series(
        [FromQuery] string metric,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var (rangeFrom, rangeTo) = NormalizeRange(from, to);
        var (failClosed, staffScope) = PortalAccessHttpContext.ResolveBookingsStaffScope(
            PortalAccessHttpContext.Get(HttpContext));
        if (failClosed)
        {
            return Ok(new TenantDashboardSeriesResponse(metric, rangeFrom, rangeTo, null, Array.Empty<TenantDashboardSeriesPoint>()));
        }

        var result = await dashboard.GetSeriesAsync(
            tenantId,
            metric,
            rangeFrom,
            rangeTo,
            staffScope,
            locationId,
            cancellationToken);

        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("breakdown")]
    public async Task<IActionResult> Breakdown(
        [FromQuery] string dimension,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var (rangeFrom, rangeTo) = NormalizeRange(from, to);
        var (failClosed, staffScope) = PortalAccessHttpContext.ResolveBookingsStaffScope(
            PortalAccessHttpContext.Get(HttpContext));
        if (failClosed)
        {
            return Ok(new TenantDashboardBreakdownResponse(dimension, rangeFrom, rangeTo, Array.Empty<TenantDashboardBreakdownItem>()));
        }

        var result = await dashboard.GetBreakdownAsync(
            tenantId,
            dimension,
            rangeFrom,
            rangeTo,
            staffScope,
            locationId,
            cancellationToken);

        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("layout")]
    public async Task<IActionResult> Layout(CancellationToken cancellationToken)
    {
        var access = PortalAccessHttpContext.Get(HttpContext);
        if (access is null)
        {
            return Unauthorized();
        }

        var practitionerView = IsPractitionerDashboardView(access);

        var result = await dashboard.GetLayoutAsync(
            access.TenantId,
            access.BusinessUserId,
            practitionerView,
            cancellationToken);

        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPut("layout")]
    public async Task<IActionResult> SaveLayout(
        [FromBody] TenantDashboardLayoutDto layout,
        CancellationToken cancellationToken)
    {
        var access = PortalAccessHttpContext.Get(HttpContext);
        if (access is null)
        {
            return Unauthorized();
        }

        var practitionerView = IsPractitionerDashboardView(access);

        var result = await dashboard.SaveLayoutAsync(
            access.TenantId,
            access.BusinessUserId,
            layout,
            practitionerView,
            cancellationToken);

        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    private Guid? ResolveTenantId() => PortalAccessHttpContext.Get(HttpContext)?.TenantId;

    private static bool IsPractitionerDashboardView(PortalAccessSnapshot access) =>
        access.Permissions.Contains(BusinessPermissions.StaffSelf)
        && !access.Permissions.Contains(BusinessPermissions.Staff);

    private static (DateTimeOffset From, DateTimeOffset To) NormalizeRange(
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var end = to ?? DateTimeOffset.UtcNow;
        var start = from ?? end.AddDays(-7);
        if (start >= end)
        {
            start = end.AddDays(-7);
        }

        return (start, end);
    }
}
