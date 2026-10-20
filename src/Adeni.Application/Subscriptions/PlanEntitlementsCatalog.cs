namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Subscriptions;

/// <summary>
/// Plan capability matrix. Keep aligned with packages/shared/src/data/plan-entitlements.json
/// and docs/specs/adeni-subscription-pricing-v1/plan-entitlements.seed.json.
/// </summary>
public static class PlanEntitlementsCatalog
{
    public static TenantEntitlements ForSku(SubscriptionSku sku)
    {
        var seed = GetSeed(sku);
        return new TenantEntitlements(
            MaxLocations: seed.MaxLocations,
            MaxPractitioners: seed.MaxPractitioners,
            Messaging: seed.Messaging,
            Waitlist: seed.Waitlist,
            WhatsappIntegration: seed.WhatsappIntegration,
            AdvancedAnalytics: seed.AdvancedAnalytics,
            CustomerRetentionAnalytics: seed.CustomerRetentionAnalytics,
            StaffPerformanceAnalytics: seed.StaffPerformanceAnalytics,
            AdvancedReminders: seed.AdvancedReminders,
            MultiLocationReporting: seed.MultiLocationReporting,
            PrioritySupport: seed.PrioritySupport,
            MultiLocation: seed.MaxLocations is null || seed.MaxLocations > 1);
    }

    private static PlanEntitlementSeed GetSeed(SubscriptionSku sku) =>
        sku switch
        {
            SubscriptionSku.Professional => Professional,
            SubscriptionSku.Business => Business,
            _ => Starter,
        };

    private static readonly PlanEntitlementSeed Starter = new(
        MaxLocations: 1,
        MaxPractitioners: 3,
        Messaging: true,
        AdvancedReminders: false,
        WhatsappIntegration: false,
        AdvancedAnalytics: false,
        CustomerRetentionAnalytics: false,
        StaffPerformanceAnalytics: false,
        Waitlist: false,
        MultiLocationReporting: false,
        PrioritySupport: false);

    private static readonly PlanEntitlementSeed Professional = new(
        MaxLocations: 1,
        MaxPractitioners: 10,
        Messaging: true,
        AdvancedReminders: true,
        WhatsappIntegration: true,
        AdvancedAnalytics: true,
        CustomerRetentionAnalytics: true,
        StaffPerformanceAnalytics: true,
        Waitlist: true,
        MultiLocationReporting: false,
        PrioritySupport: false);

    private static readonly PlanEntitlementSeed Business = new(
        MaxLocations: null,
        MaxPractitioners: null,
        Messaging: true,
        AdvancedReminders: true,
        WhatsappIntegration: true,
        AdvancedAnalytics: true,
        CustomerRetentionAnalytics: true,
        StaffPerformanceAnalytics: true,
        Waitlist: true,
        MultiLocationReporting: true,
        PrioritySupport: true);

    private sealed record PlanEntitlementSeed(
        int? MaxLocations,
        int? MaxPractitioners,
        bool Messaging,
        bool AdvancedReminders,
        bool WhatsappIntegration,
        bool AdvancedAnalytics,
        bool CustomerRetentionAnalytics,
        bool StaffPerformanceAnalytics,
        bool Waitlist,
        bool MultiLocationReporting,
        bool PrioritySupport);
}
