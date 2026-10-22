namespace Adeni.Application.Reporting;

using Adeni.Domain.Identity;

public static class ReportDefinitionRegistry
{
    private static readonly IReadOnlyDictionary<string, ReportDefinition> Definitions =
        new Dictionary<string, ReportDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [ReportKeys.BookingsRegister] = new(
                ReportKeys.BookingsRegister,
                PortalPermissions.Bookings,
                ReportBaseEntitlement.BasicExports,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv, ReportExportFormat.Xlsx }),
            [ReportKeys.Compensation] = new(
                ReportKeys.Compensation,
                PortalPermissions.Finance,
                ReportBaseEntitlement.AdvancedReports,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv, ReportExportFormat.Xlsx }),
            [ReportKeys.SalesSummary] = new(
                ReportKeys.SalesSummary,
                PortalPermissions.Finance,
                ReportBaseEntitlement.BasicExports,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv, ReportExportFormat.Pdf }),
            [ReportKeys.StaffRoster] = new(
                ReportKeys.StaffRoster,
                PortalPermissions.Staff,
                ReportBaseEntitlement.BasicExports,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv }),
        };

    public static bool TryGet(string reportKey, out ReportDefinition definition) =>
        Definitions.TryGetValue(reportKey.Trim(), out definition!);

    public static IReadOnlyCollection<ReportDefinition> All => Definitions.Values.ToArray();
}
