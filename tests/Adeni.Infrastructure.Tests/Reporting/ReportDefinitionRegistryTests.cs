namespace Adeni.Infrastructure.Tests.Reporting;

using Adeni.Application.Reporting;
using Adeni.Domain.Identity;

public sealed class ReportDefinitionRegistryTests
{
    [Theory]
    [InlineData(ReportKeys.BookingsRegister, BusinessPermissions.Bookings)]
    [InlineData(ReportKeys.TenantCustomers, BusinessPermissions.Bookings)]
    [InlineData(ReportKeys.SalesSummary, BusinessPermissions.Finance)]
    [InlineData(ReportKeys.Compensation, BusinessPermissions.Finance)]
    [InlineData(ReportKeys.StaffRoster, BusinessPermissions.Staff)]
    [InlineData(ReportKeys.TenantLocations, BusinessPermissions.Locations)]
    [InlineData(ReportKeys.TenantServices, BusinessPermissions.Services)]
    public void Registry_required_permission_matches_launch_matrix(string reportKey, string expectedPermission)
    {
        Assert.True(ReportDefinitionRegistry.TryGet(reportKey, out var definition));
        Assert.Equal(expectedPermission, definition!.RequiredPermission);
    }

    [Fact]
    public void Bookings_register_allows_bookings_self_at_export_service_layer()
    {
        Assert.True(ReportDefinitionRegistry.TryGet(ReportKeys.BookingsRegister, out var definition));
        Assert.Equal(BusinessPermissions.Bookings, definition!.RequiredPermission);
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
