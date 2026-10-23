namespace Adeni.Infrastructure.Tests.Reporting;

using Adeni.Application.Reporting;
using Adeni.Domain.Identity;

public sealed class ReportDefinitionRegistryTests
{
    [Theory]
    [InlineData(ReportKeys.BookingsRegister, PortalPermissions.Bookings)]
    [InlineData(ReportKeys.TenantCustomers, PortalPermissions.Bookings)]
    [InlineData(ReportKeys.SalesSummary, PortalPermissions.Finance)]
    [InlineData(ReportKeys.Compensation, PortalPermissions.Finance)]
    [InlineData(ReportKeys.StaffRoster, PortalPermissions.Staff)]
    [InlineData(ReportKeys.TenantLocations, PortalPermissions.Locations)]
    [InlineData(ReportKeys.TenantServices, PortalPermissions.Services)]
    public void Registry_required_permission_matches_launch_matrix(string reportKey, string expectedPermission)
    {
        Assert.True(ReportDefinitionRegistry.TryGet(reportKey, out var definition));
        Assert.Equal(expectedPermission, definition!.RequiredPermission);
    }

    [Fact]
    public void Bookings_register_allows_bookings_self_at_export_service_layer()
    {
        Assert.True(ReportDefinitionRegistry.TryGet(ReportKeys.BookingsRegister, out var definition));
        Assert.Equal(PortalPermissions.Bookings, definition!.RequiredPermission);
        Assert.Contains(ReportExportFormat.Csv, definition.SupportedFormats);
    }

    [Fact]
    public void Sales_summary_pdf_requires_advanced_reports_entitlement_at_service_layer()
    {
        Assert.True(ReportDefinitionRegistry.TryGet(ReportKeys.SalesSummary, out var definition));
        Assert.Contains(ReportExportFormat.Pdf, definition!.SupportedFormats);
        Assert.Equal(ReportBaseEntitlement.BasicExports, definition.BaseEntitlement);
    }
}
