namespace Adeni.Application.Reporting;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;

public sealed record ReportExportRequest(
    Guid TenantId,
    string ReportKey,
    DateTimeOffset From,
    DateTimeOffset To,
    ReportExportFormat Format,
    IReadOnlyList<string> PortalPermissions,
    TenantEntitlements Entitlements,
    string ActorAuth0Sub,
    Guid? LocationId = null,
    Guid? StaffMemberId = null);

public interface IReportExportService
{
    Task<Result<ReportExportPayload>> ExportAsync(
        ReportExportRequest request,
        CancellationToken cancellationToken = default);
}
