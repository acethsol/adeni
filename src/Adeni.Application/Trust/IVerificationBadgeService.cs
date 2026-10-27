namespace Adeni.Application.Trust;

using Adeni.Application.Admin;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;

public sealed record VerificationBadgeResponse(
    string BadgeType,
    string Status,
    DateTimeOffset? GrantedAt);

public sealed record RequestVerificationBadgeRequest(
    VerificationBadgeType BadgeType,
    string? ReferenceNumber);

public sealed record PendingBadgeRequestResponse(
    Guid Id,
    Guid TenantId,
    string BusinessName,
    string BadgeType,
    string? ReferenceNumber,
    DateTimeOffset RequestedAt);

public interface IVerificationBadgeService
{
    /// <summary>Granted and pending badges for the tenant portal (revoked rows omitted).</summary>
    Task<IReadOnlyList<VerificationBadgeResponse>> GetTenantBadgesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetGrantedBadgeTypesForTenantsAsync(
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken = default);

    Task<Result<VerificationBadgeResponse>> RequestBadgeAsync(
        Guid tenantId,
        string auth0Sub,
        RequestVerificationBadgeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminPagedResult<PendingBadgeRequestResponse>>> ListPendingRequestsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> GrantBadgeAsync(
        Guid tenantId,
        VerificationBadgeType badgeType,
        string adminId,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> RevokeBadgeAsync(
        Guid tenantId,
        VerificationBadgeType badgeType,
        string adminId,
        CancellationToken cancellationToken = default);

    Task GrantInitialBadgesOnApprovalAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
