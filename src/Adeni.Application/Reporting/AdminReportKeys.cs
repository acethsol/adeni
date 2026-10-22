namespace Adeni.Application.Reporting;

public static class AdminReportKeys
{
    public const string BusinessDirectory = "admin-business-directory";
    public const string PendingVerifications = "admin-pending-verifications";
    public const string Transactions = "admin-transactions";
    public const string PricingCatalogEntries = "admin-pricing-catalog-entries";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        BusinessDirectory,
        PendingVerifications,
        Transactions,
        PricingCatalogEntries,
    };

    public static bool IsKnown(string? reportKey) =>
        !string.IsNullOrWhiteSpace(reportKey) && All.Contains(reportKey.Trim());
}
