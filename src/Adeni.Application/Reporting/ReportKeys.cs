namespace Adeni.Application.Reporting;

public static class ReportKeys
{
    public const string GiftCards = "gift-cards";
    public const string BookingsRegister = "bookings-register";
    public const string Compensation = "compensation";
    public const string SalesSummary = "sales-summary";
    public const string StaffRoster = "staff-roster";
    public const string TenantCustomers = "tenant-customers";
    public const string TenantLocations = "tenant-locations";
    public const string TenantServices = "tenant-services";
    public const string RetentionMembershipMrr = "retention-membership-mrr";
    public const string AttendanceRegister = "attendance-register";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        GiftCards,
        BookingsRegister,
        Compensation,
        SalesSummary,
        StaffRoster,
        TenantCustomers,
        TenantLocations,
        TenantServices,
        RetentionMembershipMrr,
        AttendanceRegister,
    };

    public static bool IsKnown(string? reportKey) =>
        !string.IsNullOrWhiteSpace(reportKey) && All.Contains(reportKey.Trim());
}
