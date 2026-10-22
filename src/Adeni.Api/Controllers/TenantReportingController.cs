namespace Adeni.Api.Controllers;

using Adeni.Api.Auth;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Reporting;
using System.Security.Claims;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/reporting")]
public sealed class TenantReportingController(
    IReportExportService reportExportService,
    IEntitlementsService entitlementsService,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet("exports/{reportKey}")]
    [RequiresPortalPermission(
        PortalPermissions.Overview,
        PortalPermissions.Bookings,
        PortalPermissions.BookingsSelf,
        PortalPermissions.Finance,
        PortalPermissions.Staff)]
    public async Task<IActionResult> Export(
        string reportKey,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] string format = "csv",
        [FromQuery] Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        var access = PortalAccessHttpContext.Get(HttpContext);
        if (access is null)
        {
            return Unauthorized();
        }

        if (!TryParseFormat(format, out var exportFormat))
        {
            return BadRequest(new { title = "Format must be csv, xlsx, or pdf.", code = ErrorCodes.ReportFormatNotSupported });
        }

        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var entitlementsResult = await entitlementsService.GetEntitlementsForMemberAsync(
            access.TenantId,
            auth0Sub,
            cancellationToken);

        if (entitlementsResult.IsFailure)
        {
            return ApiErrorResponseMapper.ToActionResult(entitlementsResult.Error!, HttpContext);
        }

        var (_, staffScope) = PortalAccessHttpContext.ResolveBookingsStaffScope(access);

        var result = await reportExportService.ExportAsync(
            new ReportExportRequest(
                access.TenantId,
                reportKey,
                from,
                to,
                exportFormat,
                access.Permissions,
                entitlementsResult.Value!,
                locationId,
                staffScope),
            cancellationToken);

        return result.Match<IActionResult>(
            payload => File(payload.Body, payload.ContentType, payload.FileName),
            error => ApiErrorResponseMapper.ToActionResult(error, HttpContext));
    }

    private string? ResolveAuth0Sub()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return User.FindFirstValue("sub");
        }

        if (!auth0Options.Value.Enabled
            && Request.Headers.TryGetValue(DevBusinessAuthMiddleware.DevAuth0SubHeader, out var devSub)
            && !string.IsNullOrWhiteSpace(devSub))
        {
            return devSub.ToString();
        }

        return null;
    }

    private static bool TryParseFormat(string format, out ReportExportFormat exportFormat)
    {
        exportFormat = ReportExportFormat.Csv;
        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            exportFormat = ReportExportFormat.Xlsx;
            return true;
        }

        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            exportFormat = ReportExportFormat.Pdf;
            return true;
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
