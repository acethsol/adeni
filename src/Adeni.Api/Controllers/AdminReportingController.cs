namespace Adeni.Api.Controllers;

using Adeni.Api.Errors;
using Adeni.Application.Reporting;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/admin/reporting")]
[Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy)]
public sealed class AdminReportingController(
    IAdminReportingQueryService reporting,
    IAdminReportExportService reportExportService) : ControllerBase
{
    [HttpGet("platform-subscription-mrr")]
    public async Task<IActionResult> PlatformSubscriptionMrr(CancellationToken cancellationToken)
    {
        var payload = await reporting.GetPlatformSubscriptionMrrAsync(cancellationToken);
        return Ok(payload);
    }

    [HttpGet("gmv")]
    public async Task<IActionResult> Gmv(
        [FromQuery] string granularity = "month",
        CancellationToken cancellationToken = default)
    {
        var payload = await reporting.GetGmvAsync(granularity, cancellationToken);
        return Ok(payload);
    }

    [HttpGet("exports/{reportKey}")]
    public async Task<IActionResult> Export(
        string reportKey,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] string format = "csv",
        [FromQuery] string? q = null,
        [FromQuery] string? kind = null,
        [FromQuery] string? catalogVersionCode = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseFormat(format, out var exportFormat))
        {
            return BadRequest(new { title = "Format must be csv or xlsx.", code = ErrorCodes.ReportFormatNotSupported });
        }

        var result = await reportExportService.ExportAsync(
            new AdminReportExportRequest(
                reportKey,
                from,
                to,
                exportFormat,
                q,
                kind,
                catalogVersionCode),
            cancellationToken);

        return result.Match<IActionResult>(
            payload => File(payload.Body, payload.ContentType, payload.FileName),
            error => ApiErrorResponseMapper.ToActionResult(error, HttpContext));
    }

    private static bool TryParseFormat(string format, out ReportExportFormat exportFormat)
    {
        exportFormat = ReportExportFormat.Csv;
        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            exportFormat = ReportExportFormat.Xlsx;
            return true;
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
