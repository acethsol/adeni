namespace Adeni.Application.Retention;

using Adeni.Domain.Common;

/// <summary>Tenant loyalty program settings (Sprint 37.3-A). Every method requires the <c>loyaltyBasic</c> entitlement.</summary>
public interface ILoyaltyProgramService
{
    /// <summary>Saved program, or suggested defaults with <c>Configured = false</c>.</summary>
    Task<Result<LoyaltyProgramResponse>> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Owner-only create-or-replace (audited).</summary>
    Task<Result<LoyaltyProgramResponse>> UpsertAsync(
        Guid tenantId,
        RetentionActor actor,
        UpsertLoyaltyProgramRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Per-customer points ledger (Sprint 37.3-A). Earning / redeeming arrive in slices B–C; slice A only reads and
/// posts audited manual adjustments. Every method requires the <c>loyaltyBasic</c> entitlement.
/// </summary>
public interface ILoyaltyAccountService
{
    /// <summary>Zeros when the customer has no account yet (no row is created).</summary>
    Task<Result<LoyaltyAccountResponse>> GetForCustomerAsync(
        Guid tenantId,
        Guid tenantCustomerId,
        CancellationToken cancellationToken = default);

    /// <summary>Newest first; <paramref name="limit"/> is clamped to 1–200.</summary>
    Task<Result<LoyaltyTransactionListResponse>> ListTransactionsAsync(
        Guid tenantId,
        Guid tenantCustomerId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Owner / manager. Signed adjustment with a required reason; needs a saved program. Idempotent per
    /// <paramref name="idempotencyKey"/> (required).
    /// </summary>
    Task<Result<LoyaltyAdjustmentResponse>> AdjustAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid tenantCustomerId,
        AdjustLoyaltyPointsRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}
