namespace Adeni.Application.Retention;

using Adeni.Domain.Common;
using Adeni.Domain.Retention;

/// <summary>Anonymous Discover plan entry: no internal flags, no tenant ids.</summary>
public sealed record PublicMembershipPlanResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    string BillingInterval,
    string RolloverPolicy,
    string CancellationPolicy,
    bool AllowFreeze,
    int PastDueGraceDays,
    IReadOnlyList<MembershipBenefitResponse> Benefits);

/// <summary><c>OnlineSubscribeEnabled</c> is false until tenant merchant settlement is live (Sprint 34 gate) or the plan lacks recurring billing.</summary>
public sealed record PublicMembershipPlanCatalogResponse(
    IReadOnlyList<PublicMembershipPlanResponse> Items,
    bool OnlineSubscribeEnabled);

/// <summary>Result of starting an online subscription: the membership stays <c>pending</c> until <c>PaymentCompleted</c>.</summary>
public sealed record MembershipSubscribeResponse(
    Guid CustomerMembershipId,
    Guid PaymentIntentId,
    string Status,
    string? CheckoutUrl);

/// <summary>What one billing sweep did (also useful for ops logs / tests).</summary>
public sealed record MembershipBillingRunResult(
    int RenewalsStarted,
    int PastDue,
    int Expired,
    int CancellationsCompleted,
    int Reconciled);

/// <summary>One currency's normalized monthly recurring revenue from live memberships.</summary>
public sealed record TenantMembershipMrr(string Currency, decimal MonthlyRecurringRevenue, int MemberCount);

/// <summary>
/// Retention-owned membership MRR (not Adeni SaaS <c>PlatformSubscriptionMrr</c>). Per currency, never
/// converted or summed across currencies. Sprint 37.5 wraps this behind <c>IRetentionAnalyticsService</c>.
/// </summary>
public sealed record TenantMembershipMrrResponse(IReadOnlyList<TenantMembershipMrr> Items, DateTimeOffset AsOf);

/// <summary>
/// Orchestrates provider-billed memberships (Sprint 37.2-C). Retention never holds money: it asks Finance for a
/// payment intent per billing cycle and applies a cycle <b>only</b> from the idempotent <c>PaymentCompleted</c>
/// signal. Provider mandate / subscription references are stored for later settlement work (Sprint 34).
/// </summary>
public interface IMembershipBillingCoordinator
{
    /// <summary>True while online recurring checkout may run (dev/Testing + stub provider only until Sprint 34).</summary>
    bool OnlineBillingEnabled { get; }

    /// <summary>
    /// Starts (or resumes, same key / same pending plan) an online subscription: a <c>Pending</c> membership plus a
    /// first-cycle payment intent. Customer-authenticated; <paramref name="idempotencyKey"/> is required.
    /// </summary>
    Task<Result<MembershipSubscribeResponse>> StartOnlineSubscriptionAsync(
        Guid tenantId,
        string customerAuth0Sub,
        Guid membershipPlanId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PaymentCompleted</c> handler. Returns false when the intent is not a membership payment; true when it was
    /// recognised (applied, replayed, or flagged unapplied/mismatched). Never applies a cycle twice.
    /// </summary>
    Task<bool> ApplyPaymentCompletedAsync(
        Guid tenantId,
        Guid paymentIntentId,
        decimal amount,
        string currency,
        string providerReference,
        CancellationToken cancellationToken = default);

    /// <summary><c>PaymentFailed</c> handler: marks the charge failed and the membership past due.</summary>
    Task<bool> ApplyPaymentFailedAsync(
        Guid tenantId,
        Guid paymentIntentId,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scheduled sweep: closes <c>CancelPending</c> memberships at cycle end, starts renewal charges for online
    /// memberships whose cycle ended, moves unpaid ones to <c>PastDue</c> and <c>Expired</c> after the plan's grace,
    /// and reconciles confirmed-but-unapplied payments.
    /// </summary>
    Task<MembershipBillingRunResult> ProcessDueAsync(int batchSize, CancellationToken cancellationToken = default);
}

/// <summary>Discover surface for memberships: public plan list (anonymous).</summary>
public interface IMembershipStorefrontService
{
    /// <summary>Anonymous. Verified businesses only; empty when the plan has no <c>memberships</c> entitlement.</summary>
    Task<Result<PublicMembershipPlanCatalogResponse>> GetPublicPlansAsync(
        string slug,
        CancellationToken cancellationToken = default);

    /// <summary>Customer subscribe. Resolves the business from the slug, then delegates to the billing coordinator.</summary>
    Task<Result<MembershipSubscribeResponse>> SubscribeAsync(
        string customerAuth0Sub,
        string slug,
        Guid membershipPlanId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}

/// <summary><c>TenantMembershipMrr</c> calculation port (Sprint 37.2-E; wrapped by 37.5 analytics).</summary>
public interface IMembershipMrrService
{
    Task<Result<TenantMembershipMrrResponse>> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>Pure MRR math shared by the service and tests.</summary>
public static class MembershipMrrMath
{
    /// <summary>Statuses that count toward MRR: paying members, including those inside the past-due grace.</summary>
    public static readonly CustomerMembershipStatus[] CountedStatuses =
        [CustomerMembershipStatus.Active, CustomerMembershipStatus.PastDue];

    /// <summary>Price normalized to one month (quarterly / 3, annually / 12), rounded to 2 dp.</summary>
    public static decimal ToMonthly(decimal price, MembershipBillingInterval interval) =>
        Math.Round(
            interval switch
            {
                MembershipBillingInterval.Quarterly => price / 3m,
                MembershipBillingInterval.Annually => price / 12m,
                _ => price,
            },
            2,
            MidpointRounding.AwayFromZero);
}
