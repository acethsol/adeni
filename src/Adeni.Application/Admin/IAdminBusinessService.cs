namespace Adeni.Application.Admin;

using Adeni.Application.Common;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;

public sealed record PendingBusinessResponse(
    Guid Id,
    string Name,
    string Slug,
    string MarketId,
    TenantStatus Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PendingVerificationDocumentResponse> Documents);

public sealed record PendingVerificationDocumentResponse(
    string DocumentType,
    string ReferenceNumber,
    DateTimeOffset SubmittedAt);

public sealed record AdminBusinessSummaryResponse(
    Guid Id,
    string Name,
    string Slug,
    TenantStatus Status,
    string SubscriptionSku,
    string BillingStatus,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset CreatedAt);

public sealed record AdminTenantCommercialTermsResponse(
    Guid TenantId,
    string SubscriptionSku,
    string BillingStatus,
    string? TrialSku,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? PaidThrough,
    decimal? DiscountPercent,
    decimal? DiscountFixedAmount,
    string? DiscountCurrency,
    int? DiscountCyclesRemaining,
    string? BillingCompReason,
    decimal? CustomSaasMonthlyAmount,
    string? CustomSaasCurrency,
    string? CustomSaasSku,
    string? BillingCountryCode,
    decimal? CatalogListPrice,
    decimal? EffectiveMonthlyAmount,
    string? PricingCatalogVersion,
    string? Currency,
    decimal? AgreedMonthlyAmount = null,
    string? BoundPricingCatalogVersion = null,
    DateTimeOffset? PriceLockEndsAt = null);

public sealed record AdminTenantCommercialAuditEntry(
    Guid Id,
    string ActorId,
    string Action,
    DateTimeOffset OccurredAt,
    string? MetadataJson);

public sealed record SetTenantCommercialTermsRequest(
    string? BillingStatus = null,
    string? SubscriptionSku = null,
    string? TrialSku = null,
    DateTimeOffset? TrialEndsAt = null,
    int? TrialDays = null,
    bool? EndTrial = null,
    DateTimeOffset? PaidThrough = null,
    decimal? DiscountPercent = null,
    decimal? DiscountFixedAmount = null,
    string? DiscountCurrency = null,
    int? DiscountCyclesRemaining = null,
    bool? ClearDiscounts = null,
    string? BillingCompReason = null,
    decimal? CustomSaasMonthlyAmount = null,
    string? CustomSaasCurrency = null,
    string? CustomSaasSku = null,
    bool? ClearCustomSaasPrice = null,
    string? ChangeReason = null);

public interface IAdminBusinessService
{
    Task<Result<AdminPagedResult<PendingBusinessResponse>>> ListPendingVerificationsAsync(
        string? search,
        int page,
        int pageSize,
        string? sortBy = null,
        ListSortDirection? sortDirection = null,
        CancellationToken cancellationToken = default);

    Task<Result<AdminPagedResult<AdminBusinessSummaryResponse>>> ListBusinessesAsync(
        string? search,
        int page,
        int pageSize,
        string? sortBy = null,
        ListSortDirection? sortDirection = null,
        CancellationToken cancellationToken = default);

    Task<Result<AdminBusinessSummaryResponse>> GetBusinessAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<AdminBusinessDirectoryStats> GetDirectoryStatsAsync(
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> ApproveAsync(Guid tenantId, string adminId, CancellationToken cancellationToken = default);

    Task<Result<Unit>> RejectAsync(
        Guid tenantId,
        string adminId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<Result<AdminTenantCommercialTermsResponse>> GetTenantCommercialTermsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> SetTenantCommercialTermsAsync(
        Guid tenantId,
        SetTenantCommercialTermsRequest request,
        string adminId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AdminTenantCommercialAuditEntry>>> ListTenantCommercialAuditAsync(
        Guid tenantId,
        int limit = 30,
        CancellationToken cancellationToken = default);
}

public readonly record struct Unit
{
    public static Unit Value => default;
}
