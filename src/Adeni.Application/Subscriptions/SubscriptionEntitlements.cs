namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Subscriptions;

public sealed record TenantEntitlements(
    int? MaxLocations,
    int? MaxPractitioners,
    bool AdditionalLocationsAllowed,
    bool AdditionalPractitionersAllowed,
    bool UnlimitedServices,
    bool UnlimitedBookings,
    bool PublicProfile,
    bool DiscoverListing,
    bool Messaging,
    bool InvoicesReceipts,
    bool OnlinePayments,
    bool Deposits,
    bool BasicReminders,
    bool ConfigurableReminders,
    bool FollowUpAutomation,
    bool AdvancedReminders,
    bool Waitlist,
    bool SmsEnabled,
    bool SmsUsageBilling,
    bool BasicAnalytics,
    bool AdvancedAnalytics,
    bool CustomerRetentionAnalytics,
    bool StaffPerformanceAnalytics,
    bool BasicExports,
    bool AdvancedReports,
    bool ExcelExport,
    bool MultiLocationReporting,
    bool AdvancedPermissions,
    bool PrioritySupport,
    bool Packages,
    bool Memberships,
    bool RecurringMembershipBilling,
    bool LoyaltyBasic,
    bool LoyaltyTiers,
    bool MultiLocation);

public static class SubscriptionEntitlements
{
    public static TenantEntitlements ForSku(SubscriptionSku sku) =>
        PlanEntitlementsCatalog.ForSku(sku);
}
