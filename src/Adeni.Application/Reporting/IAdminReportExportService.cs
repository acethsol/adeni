namespace Adeni.Application.Reporting;

using Adeni.Domain.Common;

public sealed record AdminReportExportRequest(
    string ReportKey,
    DateTimeOffset From,
    DateTimeOffset To,
    ReportExportFormat Format,
    string? Search = null,
    string? TransactionKind = null,
    string? CatalogVersionCode = null);

public interface IAdminReportExportService
{
    Task<Result<ReportExportPayload>> ExportAsync(
        AdminReportExportRequest request,
        CancellationToken cancellationToken = default);
}
