namespace Adeni.Application.Reporting;

public static class ReportKeys
{
    public const string BookingsRegister = "bookings-register";
    public const string Compensation = "compensation";
    public const string SalesSummary = "sales-summary";
    public const string StaffRoster = "staff-roster";
    public const string RetentionMembershipMrr = "retention-membership-mrr";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        BookingsRegister,
        Compensation,
        SalesSummary,
        StaffRoster,
        RetentionMembershipMrr,
    };

    public static bool IsKnown(string? reportKey) =>
        !string.IsNullOrWhiteSpace(reportKey) && All.Contains(reportKey.Trim());
}
