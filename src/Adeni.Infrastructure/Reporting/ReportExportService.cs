namespace Adeni.Infrastructure.Reporting;

using System.Text;
using Adeni.Application.Finance;
using Adeni.Application.Reporting;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Microsoft.Extensions.Logging;

public sealed class ReportExportService(
    ITenantReportExportQueryService exportQueries,
    IStaffCompensationService staffCompensation,
    IReportRenderer reportRenderer,
    ILogger<ReportExportService> logger) : IReportExportService
{
    public async Task<Result<ReportExportPayload>> ExportAsync(
        ReportExportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.To < request.From)
        {
            return Result.Failure<ReportExportPayload>(
                Error.Validation("'to' must be on or after 'from'."));
        }

        if (!ReportDefinitionRegistry.TryGet(request.ReportKey, out var definition))
        {
            return Result.Failure<ReportExportPayload>(ErrorCodes.ReportNotFoundError(request.ReportKey));
        }

        if (!definition.SupportedFormats.Contains(request.Format))
        {
            return Result.Failure<ReportExportPayload>(
                ErrorCodes.ReportFormatNotSupportedError(request.ReportKey, request.Format.ToString().ToLowerInvariant()));
        }

        var permissionOk = definition.RequiredPermission switch
        {
            PortalPermissions.Bookings =>
                request.PortalPermissions.Contains(PortalPermissions.Bookings)
                || request.PortalPermissions.Contains(PortalPermissions.BookingsSelf),
            _ => request.PortalPermissions.Contains(definition.RequiredPermission),
        };

        if (!permissionOk)
        {
            return Result.Failure<ReportExportPayload>(ErrorCodes.PermissionDeniedError());
        }

        var entitlementError = ValidateEntitlements(definition, request.Format, request.Entitlements);
        if (entitlementError is not null)
        {
            return Result.Failure<ReportExportPayload>(entitlementError);
        }

        var locationCount = await exportQueries.GetActiveLocationCountAsync(request.TenantId, cancellationToken);
        if (locationCount > 1
            && request.LocationId is null
            && !request.Entitlements.MultiLocationReporting)
        {
            return Result.Failure<ReportExportPayload>(ErrorCodes.MultiLocationReportingNotEntitledError());
        }

        using var _ = logger.BeginScope(new Dictionary<string, object>
        {
            ["reportKey"] = request.ReportKey,
            ["tenantId"] = request.TenantId,
            ["format"] = request.Format.ToString(),
        });

        logger.LogInformation("Report export started");

        return request.ReportKey.ToLowerInvariant() switch
        {
            ReportKeys.BookingsRegister => await ExportBookingsRegisterAsync(request, cancellationToken),
            ReportKeys.Compensation => await ExportCompensationAsync(request, cancellationToken),
            ReportKeys.SalesSummary => await ExportSalesSummaryAsync(request, cancellationToken),
            _ => Result.Failure<ReportExportPayload>(ErrorCodes.ReportNotFoundError(request.ReportKey)),
        };
    }

    private static Error? ValidateEntitlements(
        ReportDefinition definition,
        ReportExportFormat format,
        TenantEntitlements entitlements)
    {
        if (definition.BaseEntitlement == ReportBaseEntitlement.BasicExports && !entitlements.BasicExports)
        {
            return ErrorCodes.ExportsNotEntitledError();
        }

        if (definition.BaseEntitlement == ReportBaseEntitlement.AdvancedReports && !entitlements.AdvancedReports)
        {
            return ErrorCodes.ExportsNotEntitledError();
        }

        if (format == ReportExportFormat.Xlsx && !entitlements.ExcelExport)
        {
            return ErrorCodes.ExcelExportNotEntitledError();
        }

        if (format == ReportExportFormat.Pdf
            && definition.ReportKey.Equals(ReportKeys.SalesSummary, StringComparison.OrdinalIgnoreCase)
            && !entitlements.AdvancedReports)
        {
            return ErrorCodes.ExportsNotEntitledError();
        }

        return null;
    }

    private async Task<Result<ReportExportPayload>> ExportBookingsRegisterAsync(
        ReportExportRequest request,
        CancellationToken cancellationToken)
    {
        var rowsResult = await exportQueries.GetBookingsRegisterAsync(
            request.TenantId,
            request.From,
            request.To,
            request.StaffMemberId,
            request.LocationId,
            cancellationToken);

        if (rowsResult.IsFailure)
        {
            return Result.Failure<ReportExportPayload>(rowsResult.Error!);
        }

        var rows = rowsResult.Value!;
        var stamp = $"{request.From:yyyyMMdd}-{request.To:yyyyMMdd}";

        return request.Format switch
        {
            ReportExportFormat.Csv => Result.Success(new ReportExportPayload(
                "text/csv",
                $"bookings-register-{stamp}.csv",
                ReportSpreadsheetBuilder.BuildBookingsRegisterCsv(rows))),
            ReportExportFormat.Xlsx => Result.Success(new ReportExportPayload(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"bookings-register-{stamp}.xlsx",
                ReportSpreadsheetBuilder.BuildBookingsRegisterXlsx(rows))),
            _ => Result.Failure<ReportExportPayload>(
                ErrorCodes.ReportFormatNotSupportedError(request.ReportKey, request.Format.ToString())),
        };
    }

    private async Task<Result<ReportExportPayload>> ExportCompensationAsync(
        ReportExportRequest request,
        CancellationToken cancellationToken)
    {
        var rowsResult = await staffCompensation.ListExportRowsAsync(
            request.TenantId,
            request.From,
            request.To,
            cancellationToken);

        if (rowsResult.IsFailure)
        {
            return Result.Failure<ReportExportPayload>(rowsResult.Error!);
        }

        var rows = rowsResult.Value!;
        var stamp = $"{request.From:yyyyMMdd}-{request.To:yyyyMMdd}";

        return request.Format switch
        {
            ReportExportFormat.Csv =>
                Result.Success(new ReportExportPayload(
                    "text/csv",
                    $"compensation-{stamp}.csv",
                    Encoding.UTF8.GetBytes(BuildCompensationCsv(rows)))),
            ReportExportFormat.Xlsx => Result.Success(new ReportExportPayload(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"compensation-{stamp}.xlsx",
                ReportSpreadsheetBuilder.BuildCompensationXlsx(rows))),
            _ => Result.Failure<ReportExportPayload>(
                ErrorCodes.ReportFormatNotSupportedError(request.ReportKey, request.Format.ToString())),
        };
    }

    private async Task<Result<ReportExportPayload>> ExportSalesSummaryAsync(
        ReportExportRequest request,
        CancellationToken cancellationToken)
    {
        var rowsResult = await exportQueries.GetSalesSummaryAsync(
            request.TenantId,
            request.From,
            request.To,
            request.StaffMemberId,
            cancellationToken);

        if (rowsResult.IsFailure)
        {
            return Result.Failure<ReportExportPayload>(rowsResult.Error!);
        }

        var rows = rowsResult.Value!;
        var stamp = $"{request.From:yyyyMMdd}-{request.To:yyyyMMdd}";

        if (request.Format == ReportExportFormat.Csv)
        {
            return Result.Success(new ReportExportPayload(
                "text/csv",
                $"sales-summary-{stamp}.csv",
                ReportSpreadsheetBuilder.BuildSalesSummaryCsv(rows)));
        }

        if (request.Format == ReportExportFormat.Pdf)
        {
            var statement = new StatementDocumentReportData(
                "Summary",
                "All customers",
                "—",
                request.From,
                request.To,
                rows.Select(r => new CustomerStatementLine(
                    request.From,
                    $"{r.Metric}: {r.Value}",
                    null,
                    r.Currency)).ToArray());

            var pdf = await reportRenderer.RenderDocumentAsync(ReportKind.CustomerStatement, statement, cancellationToken);
            if (pdf.IsFailure)
            {
                return Result.Failure<ReportExportPayload>(pdf.Error!);
            }

            return Result.Success(new ReportExportPayload(
                "application/pdf",
                $"sales-summary-{stamp}.pdf",
                pdf.Value!));
        }

        return Result.Failure<ReportExportPayload>(
            ErrorCodes.ReportFormatNotSupportedError(request.ReportKey, request.Format.ToString()));
    }

    private static string BuildCompensationCsv(IReadOnlyList<StaffCompensationExportRow> rows)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("CreatedAt,StaffMemberId,StaffDisplayName,Type,Amount,Currency,BookingId,PaymentIntentId");
        foreach (var row in rows)
        {
            builder.Append(row.CreatedAt.ToString("O"));
            builder.Append(',');
            builder.Append(row.StaffMemberId);
            builder.Append(',');
            builder.Append(row.StaffDisplayName);
            builder.Append(',');
            builder.Append(row.Type);
            builder.Append(',');
            builder.Append(row.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(row.Currency);
            builder.Append(',');
            builder.Append(row.BookingId?.ToString() ?? string.Empty);
            builder.Append(',');
            builder.AppendLine(row.PaymentIntentId?.ToString() ?? string.Empty);
        }

        return builder.ToString();
    }
}
