namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Subscriptions;

public sealed record TenantEntitlements(
    int? MaxLocations,
    int? MaxPractitioners,
    bool Messaging,
    bool Waitlist,
    bool WhatsappIntegration,
    bool AdvancedAnalytics,
    bool CustomerRetentionAnalytics,
    bool StaffPerformanceAnalytics,
    bool AdvancedReminders,
    bool MultiLocationReporting,
    bool PrioritySupport,
    bool MultiLocation);

public static class SubscriptionEntitlements
{
    public static TenantEntitlements ForSku(SubscriptionSku sku) =>
        PlanEntitlementsCatalog.ForSku(sku);
}
