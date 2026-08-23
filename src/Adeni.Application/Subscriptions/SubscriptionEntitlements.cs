namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Subscriptions;

public sealed record TenantEntitlements(
    int? MonthlyBookingLimit,
    bool BasicCalendar,
    bool Messaging,
    bool Analytics,
    bool Reminders,
    bool MultiLocation,
    bool StaffManagement,
    bool PrioritySupport);

public static class SubscriptionEntitlements
{
    public const int FreeMonthlyBookingLimit = 20;

    public static TenantEntitlements ForTier(SubscriptionTier tier) =>
        tier switch
        {
            SubscriptionTier.Pro => new TenantEntitlements(
                MonthlyBookingLimit: null,
                BasicCalendar: true,
                Messaging: true,
                Analytics: true,
                Reminders: true,
                MultiLocation: false,
                StaffManagement: false,
                PrioritySupport: false),
            SubscriptionTier.Business => new TenantEntitlements(
                MonthlyBookingLimit: null,
                BasicCalendar: true,
                Messaging: true,
                Analytics: true,
                Reminders: true,
                MultiLocation: true,
                StaffManagement: true,
                PrioritySupport: true),
            _ => new TenantEntitlements(
                MonthlyBookingLimit: FreeMonthlyBookingLimit,
                BasicCalendar: true,
                Messaging: false,
                Analytics: false,
                Reminders: false,
                MultiLocation: false,
                StaffManagement: false,
                PrioritySupport: false),
        };
}
