namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Subscriptions;

public static class TenantBillingStatusMapping
{
    public static string ToApiValue(TenantBillingStatus status) =>
        status switch
        {
            TenantBillingStatus.Active => "active",
            TenantBillingStatus.PastDue => "past_due",
            TenantBillingStatus.Suspended => "suspended",
            TenantBillingStatus.Canceled => "canceled",
            _ => "trialing",
        };

    public static TenantBillingStatus FromApiValue(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "active" => TenantBillingStatus.Active,
            "past_due" => TenantBillingStatus.PastDue,
            "suspended" => TenantBillingStatus.Suspended,
            "canceled" => TenantBillingStatus.Canceled,
            _ => TenantBillingStatus.Trialing,
        };
}
