namespace Adeni.Infrastructure.Reporting;

using System.Text;
using Adeni.Application.Reporting;
using Adeni.Domain.Common;
using Microsoft.Extensions.Logging;

public sealed class AdminReportExportService(
    IAdminReportExportQueryService exportQueries,
    ILogger<AdminReportExportService> logger) : IAdminReportExportService
{
    public async Task<Result<ReportExportPayload>> ExportAsync(
        AdminReportExportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.To < request.From)
        {
            return Result.Failure<ReportExportPayload>(
                Error.Validation("'to' must be on or after 'from'."));
        }

        if (!AdminReportDefinitionRegistry.TryGet(request.ReportKey, out var definition))
        {
            return Result.Failure<ReportExportPayload>(ErrorCodes.ReportNotFoundError(request.ReportKey));
        }

        if (!definition.SupportedFormats.Contains(request.Format))
        {
            return Result.Failure<ReportExportPayload>(
                ErrorCodes.ReportFormatNotSupportedError(
                    request.ReportKey,
                    request.Format.ToString().ToLowerInvariant()));
        }

        using var _ = logger.BeginScope(new Dictionary<string, object>
        {
            ["reportKey"] = request.ReportKey,
            ["format"] = request.Format.ToString(),
            ["adminExport"] = true,
        });

        logger.LogInformation("Admin report export started");

        return request.ReportKey.ToLowerInvariant() switch
        {
            AdminReportKeys.BusinessDirectory => await ExportBusinessDirectoryAsync(request, cancellationToken),
            AdminReportKeys.PendingVerifications => await ExportPendingVerificationsAsync(request, cancellationToken),
            AdminReportKeys.Transactions => await ExportTransactionsAsync(request, cancellationToken),
            AdminReportKeys.PricingCatalogEntries => await ExportPricingCatalogEntriesAsync(request, cancellationToken),
            _ => Result.Failure<ReportExportPayload>(ErrorCodes.ReportNotFoundError(request.ReportKey)),
        };
    }

    private async Task<Result<ReportExportPayload>> ExportBusinessDirectoryAsync(
        AdminReportExportRequest request,
        CancellationToken cancellationToken)
    {
        var rowsResult = await exportQueries.GetBusinessDirectoryAsync(request.Search, cancellationToken);
        if (rowsResult.IsFailure)
        {
            return Result.Failure<ReportExportPayload>(rowsResult.Error!);
        }

        var rows = rowsResult.Value!;
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

        return request.Format switch
        {
            ReportExportFormat.Csv => Result.Success(new ReportExportPayload(
                "text/csv",
                $"admin-business-directory-{stamp}.csv",
                Encoding.UTF8.GetBytes(ReportSpreadsheetBuilder.BuildAdminBusinessDirectoryCsv(rows)))),
            ReportExportFormat.Xlsx => Result.Success(new ReportExportPayload(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"admin-business-directory-{stamp}.xlsx",
                ReportSpreadsheetBuilder.BuildAdminBusinessDirectoryXlsx(rows))),
            _ => Result.Failure<ReportExportPayload>(
                ErrorCodes.ReportFormatNotSupportedError(request.ReportKey, request.Format.ToString())),
        };
    }

    private async Task<Result<ReportExportPayload>> ExportPendingVerificationsAsync(
        AdminReportExportRequest request,
        CancellationToken cancellationToken)
    {
        var rowsResult = await exportQueries.GetPendingVerificationsAsync(request.Search, cancellationToken);
        if (rowsResult.IsFailure)
        {
            return Result.Failure<ReportExportPayload>(rowsResult.Error!);
        }

        var rows = rowsResult.Value!;
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

        return request.Format switch
        {
            ReportExportFormat.Csv => Result.Success(new ReportExportPayload(
                "text/csv",
                $"admin-pending-verifications-{stamp}.csv",
                Encoding.UTF8.GetBytes(ReportSpreadsheetBuilder.BuildAdminPendingVerificationsCsv(rows)))),
            ReportExportFormat.Xlsx => Result.Success(new ReportExportPayload(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"admin-pending-verifications-{stamp}.xlsx",
                ReportSpreadsheetBuilder.BuildAdminPendingVerificationsXlsx(rows))),
            _ => Result.Failure<ReportExportPayload>(
                ErrorCodes.ReportFormatNotSupportedError(request.ReportKey, request.Format.ToString())),
        };
    }

    private async Task<Result<ReportExportPayload>> ExportTransactionsAsync(
        AdminReportExportRequest request,
        CancellationToken cancellationToken)
    {
        var rowsResult = await exportQueries.GetTransactionsAsync(
            request.Search,
            request.TransactionKind,
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
            ReportExportFormat.Csv => Result.Success(new ReportExportPayload(
                "text/csv",
                $"admin-transactions-{stamp}.csv",
                Encoding.UTF8.GetBytes(ReportSpreadsheetBuilder.BuildAdminTransactionsCsv(rows)))),
            ReportExportFormat.Xlsx => Result.Success(new ReportExportPayload(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"admin-transactions-{stamp}.xlsx",
                ReportSpreadsheetBuilder.BuildAdminTransactionsXlsx(rows))),
            _ => Result.Failure<ReportExportPayload>(
                ErrorCodes.ReportFormatNotSupportedError(request.ReportKey, request.Format.ToString())),
        };
    }

    private async Task<Result<ReportExportPayload>> ExportPricingCatalogEntriesAsync(
        AdminReportExportRequest request,
        CancellationToken cancellationToken)
    {
        var rowsResult = await exportQueries.GetPricingCatalogEntriesAsync(
            request.CatalogVersionCode ?? string.Empty,
            cancellationToken);

        if (rowsResult.IsFailure)
        {
            return Result.Failure<ReportExportPayload>(rowsResult.Error!);
        }

        var rows = rowsResult.Value!;
        var code = request.CatalogVersionCode?.Trim() ?? "catalog";
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

        return request.Format switch
        {
            ReportExportFormat.Csv => Result.Success(new ReportExportPayload(
                "text/csv",
                $"admin-pricing-catalog-{code}-{stamp}.csv",
                Encoding.UTF8.GetBytes(ReportSpreadsheetBuilder.BuildAdminPricingCatalogEntriesCsv(rows)))),
            ReportExportFormat.Xlsx => Result.Success(new ReportExportPayload(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"admin-pricing-catalog-{code}-{stamp}.xlsx",
                ReportSpreadsheetBuilder.BuildAdminPricingCatalogEntriesXlsx(rows))),
            _ => Result.Failure<ReportExportPayload>(
                ErrorCodes.ReportFormatNotSupportedError(request.ReportKey, request.Format.ToString())),
        };
    }
}
