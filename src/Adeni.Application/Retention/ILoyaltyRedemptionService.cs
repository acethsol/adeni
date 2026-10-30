namespace Adeni.Application.Retention;

using Adeni.Domain.Common;

/// <summary>
/// Loyalty redemption at booking checkout (Sprint 37.3-C). Stages ledger rows on the caller's unit of work.
/// </summary>
public interface ILoyaltyRedemptionService
{
    Task<Result<LoyaltyRedeemQuoteResponse>> QuoteAsync(
        Guid tenantId,
        Guid platformCustomerId,
        int pointsToRedeem,
        decimal payableAfterPackageCredits,
        string currency,
        CancellationToken cancellationToken = default);

    /// <summary>Idempotent per booking. Returns points debited.</summary>
    Task<Result<int>> StageRedeemForBookingAsync(
        StageLoyaltyRedeemRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ReleaseRedeemForBookingAsync(
        Guid tenantId,
        Guid bookingId,
        string actor,
        CancellationToken cancellationToken = default);

    Task<decimal> GetDiscountAmountForBookingAsync(
        Guid tenantId,
        Guid bookingId,
        CancellationToken cancellationToken = default);
}

public interface ILoyaltyCustomerService
{
    Task<Result<CustomerLoyaltyListResponse>> ListForPlatformCustomerAsync(
        string customerAuth0Sub,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerLoyaltyAtBusinessResponse>> GetAtBusinessAsync(
        string customerAuth0Sub,
        string businessSlug,
        CancellationToken cancellationToken = default);

    Task<Result<LoyaltyRedeemQuoteResponse>> QuoteAtBusinessAsync(
        string customerAuth0Sub,
        string businessSlug,
        int pointsToRedeem,
        decimal payableAfterPackageCredits,
        string currency,
        CancellationToken cancellationToken = default);
}

public interface ILoyaltyExpiryService
{
    Task<int> ExpireDuePointsAsync(int batchSize, CancellationToken cancellationToken = default);
}
