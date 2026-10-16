namespace Adeni.Api.Controllers;

using Adeni.Api.Auth;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Dashboard;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/tenant/dashboard")]
[RequiresPortalPermission(PortalPermissions.Overview)]
public sealed class TenantDashboardController(ITenantDashboardService dashboard) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
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
            cancellationToken);

        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("series")]
    public async Task<IActionResult> Series(
        [FromQuery] string metric,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
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
            cancellationToken);

        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("breakdown")]
    public async Task<IActionResult> Breakdown(
        [FromQuery] string dimension,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
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
            cancellationToken);

        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    private Guid? ResolveTenantId() => PortalAccessHttpContext.Get(HttpContext)?.TenantId;

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
