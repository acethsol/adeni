namespace Adeni.Application.Reporting;

public static class AdminReportDefinitionRegistry
{
    private static readonly IReadOnlyDictionary<string, AdminReportDefinition> Definitions =
        new Dictionary<string, AdminReportDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [AdminReportKeys.BusinessDirectory] = new(
                AdminReportKeys.BusinessDirectory,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv, ReportExportFormat.Xlsx }),
            [AdminReportKeys.PendingVerifications] = new(
                AdminReportKeys.PendingVerifications,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv, ReportExportFormat.Xlsx }),
            [AdminReportKeys.Transactions] = new(
                AdminReportKeys.Transactions,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv, ReportExportFormat.Xlsx }),
            [AdminReportKeys.PricingCatalogEntries] = new(
                AdminReportKeys.PricingCatalogEntries,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv, ReportExportFormat.Xlsx }),
        };

    public static bool TryGet(string reportKey, out AdminReportDefinition definition) =>
        Definitions.TryGetValue(reportKey.Trim(), out definition!);
}

public sealed record AdminReportDefinition(
    string ReportKey,
    IReadOnlySet<ReportExportFormat> SupportedFormats);
