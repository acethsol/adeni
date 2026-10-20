namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;

/// <summary>
/// Trial expiry, grace, and suspension transitions (Sprint 33a Phase 2).
/// </summary>
public static class TenantBillingLifecycle
{
    public const int PastDueGraceDays = 7;

    public static bool ApplyPendingTransitions(Tenant tenant, DateTimeOffset utcNow)
    {
        var changed = false;

        if (tenant.BillingStatus == TenantBillingStatus.Trialing
            && tenant.TrialEndsAt is { } trialEnd
            && trialEnd <= utcNow)
        {
            tenant.BillingStatus = TenantBillingStatus.PastDue;
            tenant.TrialSku = null;
            changed = true;
        }

        if (tenant.BillingStatus == TenantBillingStatus.PastDue
            && IsPastGrace(tenant, utcNow))
        {
            tenant.BillingStatus = TenantBillingStatus.Suspended;
            changed = true;
        }

        return changed;
    }

    public static void MarkPaymentSucceeded(Tenant tenant, DateTimeOffset occurredAt)
    {
        tenant.BillingStatus = TenantBillingStatus.Active;
        tenant.TrialEndsAt = null;
        tenant.TrialSku = null;
        tenant.PaidThrough = occurredAt.AddMonths(1);
    }

    public static void MarkPaymentFailed(Tenant tenant, DateTimeOffset utcNow)
    {
        tenant.BillingStatus = TenantBillingStatus.PastDue;
        tenant.PaidThrough ??= utcNow;
    }

    private static bool IsPastGrace(Tenant tenant, DateTimeOffset utcNow)
    {
        var anchor = tenant.PaidThrough ?? tenant.TrialEndsAt;
        if (anchor is null)
        {
            return false;
        }

        return anchor.Value.AddDays(PastDueGraceDays) <= utcNow;
    }
}
