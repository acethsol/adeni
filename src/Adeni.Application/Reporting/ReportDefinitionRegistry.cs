namespace Adeni.Application.Reporting;

using Adeni.Domain.Identity;

public static class ReportDefinitionRegistry
{
    private static readonly IReadOnlyDictionary<string, ReportDefinition> Definitions =
        new Dictionary<string, ReportDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [ReportKeys.GiftCards] = new(ReportKeys.GiftCards, BusinessPermissions.Finance, ReportBaseEntitlement.BasicExports,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv }, RequiresMultiLocationReportingGate: true),
            [ReportKeys.BookingsRegister] = new(
                ReportKeys.BookingsRegister,
                BusinessPermissions.Bookings,
                ReportBaseEntitlement.BasicExports,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv, ReportExportFormat.Xlsx },
                RequiresMultiLocationReportingGate: true),
            [ReportKeys.Compensation] = new(
                ReportKeys.Compensation,
                BusinessPermissions.Finance,
                ReportBaseEntitlement.AdvancedReports,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv, ReportExportFormat.Xlsx },
                RequiresMultiLocationReportingGate: true),
            [ReportKeys.SalesSummary] = new(
                ReportKeys.SalesSummary,
                BusinessPermissions.Finance,
                ReportBaseEntitlement.BasicExports,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv, ReportExportFormat.Pdf },
                RequiresMultiLocationReportingGate: true),
            [ReportKeys.StaffRoster] = new(
                ReportKeys.StaffRoster,
                BusinessPermissions.Staff,
                ReportBaseEntitlement.BasicExports,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv }),
            [ReportKeys.TenantCustomers] = new(
                ReportKeys.TenantCustomers,
                BusinessPermissions.Bookings,
                ReportBaseEntitlement.BasicExports,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv, ReportExportFormat.Xlsx }),
            [ReportKeys.TenantLocations] = new(
                ReportKeys.TenantLocations,
                BusinessPermissions.Locations,
                ReportBaseEntitlement.BasicExports,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv }),
            [ReportKeys.TenantServices] = new(
                ReportKeys.TenantServices,
                BusinessPermissions.Services,
                ReportBaseEntitlement.BasicExports,
                new HashSet<ReportExportFormat> { ReportExportFormat.Csv, ReportExportFormat.Xlsx }),
        };

    public static bool TryGet(string reportKey, out ReportDefinition definition) =>
        Definitions.TryGetValue(reportKey.Trim(), out definition!);

    public static IReadOnlyCollection<ReportDefinition> All => Definitions.Values.ToArray();
}
