namespace Adeni.Domain.Subscriptions;

public enum TenantBillingStatus
{
    Trialing = 0,
    Active = 1,
    PastDue = 2,
    Suspended = 3,
    Canceled = 4,
}
