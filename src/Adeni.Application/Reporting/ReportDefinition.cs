namespace Adeni.Application.Reporting;

public enum ReportBaseEntitlement
{
    BasicExports = 0,
    AdvancedReports = 1,
}

public sealed record ReportDefinition(
    string ReportKey,
    string RequiredPermission,
    ReportBaseEntitlement BaseEntitlement,
    IReadOnlySet<ReportExportFormat> SupportedFormats,
    bool RequiresMultiLocationReportingGate = false,
    bool RequiresAdvancedRetentionAnalytics = false);
