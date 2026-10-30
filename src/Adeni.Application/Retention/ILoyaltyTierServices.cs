namespace Adeni.Application.Retention;

using Adeni.Domain.Common;

public sealed record LoyaltyTierResponse(
    Guid Id,
    string Name,
    int MinLifetimeEarned,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record LoyaltyTierListResponse(IReadOnlyList<LoyaltyTierResponse> Items);

public sealed record UpsertLoyaltyTierRequest(string Name, int MinLifetimeEarned, int SortOrder);

public sealed record ReplaceLoyaltyTiersRequest(IReadOnlyList<UpsertLoyaltyTierRequest> Tiers);

public sealed record LoyaltyRewardResponse(
    Guid Id,
    string Name,
    string? Description,
    string Kind,
    int PointsCost,
    Guid? ServiceOfferingId,
    bool IsActive,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record LoyaltyRewardListResponse(IReadOnlyList<LoyaltyRewardResponse> Items);

public sealed record CreateLoyaltyRewardRequest(
    string Name,
    string? Description,
    string Kind,
    int PointsCost,
    Guid? ServiceOfferingId,
    int SortOrder);

public sealed record UpdateLoyaltyRewardRequest(
    string Name,
    string? Description,
    int PointsCost,
    Guid? ServiceOfferingId,
    bool IsActive,
    int SortOrder);

public sealed record LoyaltyRewardRedemptionResponse(
    Guid Id,
    Guid LoyaltyRewardId,
    int PointsSpent,
    DateTimeOffset IssuedAt,
    LoyaltyAccountResponse Account,
    LoyaltyTransactionResponse Transaction);

public interface ILoyaltyTierService
{
    Task<Result<LoyaltyTierListResponse>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<Result<LoyaltyTierListResponse>> ReplaceAsync(
        Guid tenantId,
        RetentionActor actor,
        ReplaceLoyaltyTiersRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Recomputes tier from lifetime earned; emits <c>LoyaltyTierChanged</c> when it changes.</summary>
    Task EvaluateAfterEarnAsync(
        Guid tenantId,
        Guid loyaltyAccountId,
        CancellationToken cancellationToken = default);
}

public interface ILoyaltyRewardCatalogService
{
    Task<Result<LoyaltyRewardListResponse>> ListAsync(
        Guid tenantId,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<Result<LoyaltyRewardResponse>> CreateAsync(
        Guid tenantId,
        RetentionActor actor,
        CreateLoyaltyRewardRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<LoyaltyRewardResponse>> UpdateAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid rewardId,
        UpdateLoyaltyRewardRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<LoyaltyRewardRedemptionResponse>> RedeemForCustomerAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid tenantCustomerId,
        Guid rewardId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}
