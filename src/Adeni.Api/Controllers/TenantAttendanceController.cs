namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Booking;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/tenant/attendance")]
public sealed class TenantAttendanceController(IStaffAttendanceService attendance) : ControllerBase
{
    [HttpGet("me/today")]
    [RequiresBusinessPermission(BusinessPermissions.AttendanceSelf)]
    public async Task<IActionResult> GetMyToday(CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var access = PortalAccessHttpContext.Get(HttpContext);
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await attendance.GetMyTodayAsync(tenantId, access, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("me/schedule-today")]
    [RequiresBusinessPermission(BusinessPermissions.AttendanceSelf)]
    public async Task<IActionResult> GetMyScheduleToday(CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var access = PortalAccessHttpContext.Get(HttpContext);
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await attendance.GetMyScheduleTodayAsync(tenantId, access, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("me/events")]
    [RequiresBusinessPermission(BusinessPermissions.AttendanceSelf)]
    public async Task<IActionResult> RecordMyEvent(
        [FromBody] RecordStaffAttendanceEventRequest request,
        CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var access = PortalAccessHttpContext.Get(HttpContext);
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await attendance.RecordMyEventAsync(tenantId, access, request, cancellationToken);
        return ApiResults.FromResult(
            result,
            payload => Created($"/api/v1/tenant/attendance/me/today", payload),
            HttpContext);
    }

    [HttpPost("staff/{staffMemberId:guid}/override")]
    [RequiresBusinessPermission(BusinessPermissions.Attendance)]
    public async Task<IActionResult> RecordManagerOverride(
        Guid staffMemberId,
        [FromBody] ManagerStaffAttendanceOverrideRequest request,
        CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var access = PortalAccessHttpContext.Get(HttpContext);
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await attendance.RecordManagerOverrideAsync(tenantId, access, staffMemberId, request, cancellationToken);
        return ApiResults.FromResult(
            result,
            payload => Created($"/api/v1/tenant/attendance/me/today", payload),
            HttpContext);
    }

    [HttpGet("team")]
    [RequiresBusinessPermission(BusinessPermissions.Attendance)]
    public async Task<IActionResult> GetTeam(
        [FromQuery] DateOnly? date,
        [FromQuery] Guid? locationId,
        CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var access = PortalAccessHttpContext.Get(HttpContext);
        if (access is null)
        {
            return Unauthorized();
        }

        var day = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await attendance.GetTeamAsync(tenantId, access, day, locationId, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("staff/{staffMemberId:guid}")]
    [RequiresBusinessPermission(BusinessPermissions.AttendanceSelf, BusinessPermissions.Attendance)]
    public async Task<IActionResult> GetStaffHistory(
        Guid staffMemberId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var access = PortalAccessHttpContext.Get(HttpContext);
        if (access is null)
        {
            return Unauthorized();
        }

        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-6);
        var result = await attendance.GetStaffHistoryAsync(tenantId, access, staffMemberId, start, end, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("staff/{staffMemberId:guid}/corrections")]
    [RequiresBusinessPermission(BusinessPermissions.Attendance)]
    public async Task<IActionResult> AppendCorrection(
        Guid staffMemberId,
        [FromBody] AppendStaffAttendanceCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var access = PortalAccessHttpContext.Get(HttpContext);
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await attendance.AppendStaffCorrectionAsync(tenantId, access, staffMemberId, request, cancellationToken);
        return ApiResults.FromResult(
            result,
            payload => Created($"/api/v1/tenant/attendance/staff/{staffMemberId}", payload),
            HttpContext);
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
