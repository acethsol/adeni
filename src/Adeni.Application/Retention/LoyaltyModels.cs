namespace Adeni.Application.Retention;

/// <summary>
/// Full-replace body for the tenant loyalty program (Sprint 37.3-A). Earn <c>EarnPoints</c> per <c>EarnPerAmount</c>
/// spent; <c>RedeemPoints</c> points are worth <c>RedeemValue</c> off. Slice A stores these; B–C apply them.
/// </summary>
public sealed record UpsertLoyaltyProgramRequest(
    bool IsEnabled,
    string Currency,
    int EarnPoints,
    decimal EarnPerAmount,
    int RedeemPoints,
    decimal RedeemValue,
    int MinRedeemPoints,
    int? PointsExpiryMonths = null);

/// <summary>
/// Program settings. When the tenant has not saved a program yet the service returns the suggested defaults with
/// <c>Configured = false</c>, <c>IsEnabled = false</c> and null id / timestamps.
/// </summary>
public sealed record LoyaltyProgramResponse(
    bool Configured,
    Guid? Id,
    bool IsEnabled,
    string Currency,
    int EarnPoints,
    decimal EarnPerAmount,
    int RedeemPoints,
    decimal RedeemValue,
    int MinRedeemPoints,
    int? PointsExpiryMonths,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record LoyaltyAccountResponse(
    Guid TenantCustomerId,
    int PointsBalance,
    int LifetimeEarned,
    int LifetimeRedeemed,
    DateTimeOffset? UpdatedAt);

public sealed record LoyaltyTransactionResponse(
    Guid Id,
    int Sequence,
    string Kind,
    int Points,
    int BalanceAfter,
    string? Reason,
    Guid? BookingId,
    DateTimeOffset OccurredAt);

public sealed record LoyaltyTransactionListResponse(IReadOnlyList<LoyaltyTransactionResponse> Items);

/// <summary>Manual ± adjustment. <c>Reason</c> is required (audited).</summary>
public sealed record AdjustLoyaltyPointsRequest(int Points, string Reason);

public sealed record LoyaltyAdjustmentResponse(LoyaltyAccountResponse Account, LoyaltyTransactionResponse Transaction);

public static class LoyaltyCodes
{
    public const string Earn = "earn";
    public const string Redeem = "redeem";
    public const string Adjust = "adjust";
    public const string Expire = "expire";
}

/// <summary>Quote for applying points at booking checkout (Sprint 37.3-C).</summary>
public sealed record LoyaltyRedeemQuoteResponse(
    int PointsApplied,
    decimal DiscountAmount,
    string Currency,
    int PointsBalance,
    int RedeemPoints,
    decimal RedeemValue,
    int MinRedeemPoints);

public sealed record StageLoyaltyRedeemRequest(
    Guid TenantId,
    Guid PlatformCustomerId,
    Guid BookingId,
    int PointsToRedeem,
    decimal PayableAfterPackageCredits,
    string Actor,
    DateTimeOffset At);

/// <summary>Customer's points at one business (Discover booking + my account).</summary>
public sealed record CustomerLoyaltyAtBusinessResponse(
    Guid TenantId,
    string? BusinessName,
    string? BusinessSlug,
    int PointsBalance,
    bool ProgramEnabled,
    string Currency,
    int RedeemPoints,
    decimal RedeemValue,
    int MinRedeemPoints);

public sealed record CustomerLoyaltyListResponse(IReadOnlyList<CustomerLoyaltyAtBusinessResponse> Items);
