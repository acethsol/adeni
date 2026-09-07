namespace Adeni.Application.Admin;

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
    string SubscriptionTier,
    DateTimeOffset CreatedAt);

public sealed record SetSubscriptionTierRequest(string Tier);

public interface IAdminBusinessService
{
    Task<IReadOnlyList<PendingBusinessResponse>> GetPendingVerificationsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminBusinessSummaryResponse>> ListBusinessesAsync(
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> ApproveAsync(Guid tenantId, string adminId, CancellationToken cancellationToken = default);

    Task<Result<Unit>> RejectAsync(
        Guid tenantId,
        string adminId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> SetSubscriptionTierAsync(
        Guid tenantId,
        string tier,
        string adminId,
        CancellationToken cancellationToken = default);
}

public readonly record struct Unit
{
    public static Unit Value => default;
}
