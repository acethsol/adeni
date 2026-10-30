namespace Adeni.Infrastructure.Subscriptions;

using System.Text.Json;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Subscriptions;
using Microsoft.Extensions.Hosting;

public sealed class JsonPlanEntitlementsMatrix : IPlanEntitlementsMatrix
{
    private readonly IReadOnlyDictionary<SubscriptionSku, PlanEntitlementSeed> Seeds;

    public JsonPlanEntitlementsMatrix(IHostEnvironment environment)
    {
        Seeds = LoadSeeds(environment);
        PlanEntitlementsCatalog.Initialize(this);
    }

    public TenantEntitlements ForSku(SubscriptionSku sku)
    {
        if (!Seeds.TryGetValue(sku, out var seed))
        {
            throw new ArgumentOutOfRangeException(nameof(sku), sku, "Unknown subscription SKU.");
        }

        return MapSeed(seed);
    }

    private static IReadOnlyDictionary<SubscriptionSku, PlanEntitlementSeed> LoadSeeds(IHostEnvironment environment)
    {
        var root = PlanEntitlementsCatalogJson.ReadRoot(environment);
        return new Dictionary<SubscriptionSku, PlanEntitlementSeed>
        {
            [SubscriptionSku.Starter] = ParseSeed(root.GetProperty("starter")),
            [SubscriptionSku.Professional] = ParseSeed(root.GetProperty("professional")),
            [SubscriptionSku.Business] = ParseSeed(root.GetProperty("business")),
        };
    }

    private static PlanEntitlementSeed ParseSeed(JsonElement row) =>
        new(
            MaxLocations: row.GetProperty("maxLocations").ValueKind == JsonValueKind.Null
                ? null
                : row.GetProperty("maxLocations").GetInt32(),
            MaxPractitioners: row.GetProperty("maxPractitioners").ValueKind == JsonValueKind.Null
                ? null
                : row.GetProperty("maxPractitioners").GetInt32(),
            AdditionalLocationsAllowed: row.GetProperty("additionalLocationsAllowed").GetBoolean(),
            AdditionalPractitionersAllowed: row.GetProperty("additionalPractitionersAllowed").GetBoolean(),
            UnlimitedServices: row.GetProperty("unlimitedServices").GetBoolean(),
            UnlimitedBookings: row.GetProperty("unlimitedBookings").GetBoolean(),
            PublicProfile: row.GetProperty("publicProfile").GetBoolean(),
            DiscoverListing: row.GetProperty("discoverListing").GetBoolean(),
            Messaging: row.GetProperty("messaging").GetBoolean(),
            BasicReminders: row.GetProperty("basicReminders").GetBoolean(),
            ConfigurableReminders: row.GetProperty("configurableReminders").GetBoolean(),
            FollowUpAutomation: row.GetProperty("followUpAutomation").GetBoolean(),
            SmsEnabled: row.GetProperty("smsEnabled").GetBoolean(),
            SmsUsageBilling: row.GetProperty("smsUsageBilling").GetBoolean(),
            BasicAnalytics: row.GetProperty("basicAnalytics").GetBoolean(),
            AdvancedAnalytics: row.GetProperty("advancedAnalytics").GetBoolean(),
            CustomerRetentionAnalytics: row.GetProperty("customerRetentionAnalytics").GetBoolean(),
            StaffPerformanceAnalytics: row.GetProperty("staffPerformanceAnalytics").GetBoolean(),
            BasicExports: row.GetProperty("basicExports").GetBoolean(),
            AdvancedReports: row.GetProperty("advancedReports").GetBoolean(),
            ExcelExport: row.GetProperty("excelExport").GetBoolean(),
            Waitlist: row.GetProperty("waitlist").GetBoolean(),
            MultiLocationReporting: row.GetProperty("multiLocationReporting").GetBoolean(),
            AdvancedPermissions: row.GetProperty("advancedPermissions").GetBoolean(),
            PrioritySupport: row.GetProperty("prioritySupport").GetBoolean(),
            Packages: row.GetProperty("packages").GetBoolean(),
            Memberships: row.GetProperty("memberships").GetBoolean(),
            RecurringMembershipBilling: row.GetProperty("recurringMembershipBilling").GetBoolean(),
            LoyaltyBasic: row.GetProperty("loyaltyBasic").GetBoolean(),
            LoyaltyTiers: row.GetProperty("loyaltyTiers").GetBoolean(),
            Referrals: row.GetProperty("referrals").GetBoolean());

    private static TenantEntitlements MapSeed(PlanEntitlementSeed seed) =>
        new(
            MaxLocations: seed.MaxLocations,
            MaxPractitioners: seed.MaxPractitioners,
            AdditionalLocationsAllowed: seed.AdditionalLocationsAllowed,
            AdditionalPractitionersAllowed: seed.AdditionalPractitionersAllowed,
            UnlimitedServices: seed.UnlimitedServices,
            UnlimitedBookings: seed.UnlimitedBookings,
            PublicProfile: seed.PublicProfile,
            DiscoverListing: seed.DiscoverListing,
            Messaging: seed.Messaging,
            InvoicesReceipts: true,
            OnlinePayments: true,
            Deposits: true,
            BasicReminders: seed.BasicReminders,
            ConfigurableReminders: seed.ConfigurableReminders,
            FollowUpAutomation: seed.FollowUpAutomation,
            AdvancedReminders: seed.ConfigurableReminders || seed.FollowUpAutomation,
            Waitlist: seed.Waitlist,
            SmsEnabled: seed.SmsEnabled,
            SmsUsageBilling: seed.SmsUsageBilling,
            BasicAnalytics: seed.BasicAnalytics,
            AdvancedAnalytics: seed.AdvancedAnalytics,
            CustomerRetentionAnalytics: seed.CustomerRetentionAnalytics,
            StaffPerformanceAnalytics: seed.StaffPerformanceAnalytics,
            BasicExports: seed.BasicExports,
            AdvancedReports: seed.AdvancedReports,
            ExcelExport: seed.ExcelExport,
            MultiLocationReporting: seed.MultiLocationReporting,
            AdvancedPermissions: seed.AdvancedPermissions,
            PrioritySupport: seed.PrioritySupport,
            Packages: seed.Packages,
            Memberships: seed.Memberships,
            RecurringMembershipBilling: seed.RecurringMembershipBilling,
            LoyaltyBasic: seed.LoyaltyBasic,
            LoyaltyTiers: seed.LoyaltyTiers,
            Referrals: seed.Referrals,
            MultiLocation: seed.MaxLocations is null || seed.MaxLocations > 1);

    private sealed record PlanEntitlementSeed(
        int? MaxLocations,
        int? MaxPractitioners,
        bool AdditionalLocationsAllowed,
        bool AdditionalPractitionersAllowed,
        bool UnlimitedServices,
        bool UnlimitedBookings,
        bool PublicProfile,
        bool DiscoverListing,
        bool Messaging,
        bool BasicReminders,
        bool ConfigurableReminders,
        bool FollowUpAutomation,
        bool SmsEnabled,
        bool SmsUsageBilling,
        bool BasicAnalytics,
        bool AdvancedAnalytics,
        bool CustomerRetentionAnalytics,
        bool StaffPerformanceAnalytics,
        bool BasicExports,
        bool AdvancedReports,
        bool ExcelExport,
        bool Waitlist,
        bool MultiLocationReporting,
        bool AdvancedPermissions,
        bool PrioritySupport,
        bool Packages,
        bool Memberships,
        bool RecurringMembershipBilling,
        bool LoyaltyBasic,
        bool LoyaltyTiers,
        bool Referrals);
}
