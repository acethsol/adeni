namespace Adeni.Infrastructure.Trust;

using Adeni.Application.Admin;
using Adeni.Application.Trust;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class VerificationBadgeService(AdeniDbContext dbContext) : IVerificationBadgeService
{
    public async Task<IReadOnlyList<VerificationBadgeResponse>> GetGrantedBadgesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var badges = await dbContext.TenantVerificationBadges
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Status == VerificationBadgeStatus.Granted)
            .OrderBy(x => x.BadgeType)
            .ToListAsync(cancellationToken);

        return badges.Select(Map).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetGrantedBadgeTypesForTenantsAsync(
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken = default)
    {
        if (tenantIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var rows = await dbContext.TenantVerificationBadges
            .AsNoTracking()
            .Where(x => tenantIds.Contains(x.TenantId) && x.Status == VerificationBadgeStatus.Granted)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.TenantId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(x => ToApiBadgeType(x.BadgeType))
                    .Distinct()
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList());
    }

    public async Task<Result<VerificationBadgeResponse>> RequestBadgeAsync(
        Guid tenantId,
        string auth0Sub,
        RequestVerificationBadgeRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await HasBusinessAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (!access)
        {
            return Result.Failure<VerificationBadgeResponse>(Error.Forbidden("You do not have access to this business."));
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        if (tenant is null || tenant.Status != TenantStatus.Verified)
        {
            return Result.Failure<VerificationBadgeResponse>(
                Error.Validation("Badge upgrades are available after your business is verified."));
        }

        var existing = await dbContext.TenantVerificationBadges
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.BadgeType == request.BadgeType,
                cancellationToken);

        if (existing?.Status == VerificationBadgeStatus.Granted)
        {
            return Result.Failure<VerificationBadgeResponse>(Error.Conflict("This badge is already granted."));
        }

        if (existing?.Status == VerificationBadgeStatus.Pending)
        {
            return Result.Failure<VerificationBadgeResponse>(Error.Conflict("This badge is already pending review."));
        }

        var badge = new TenantVerificationBadge
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BadgeType = request.BadgeType,
            Status = VerificationBadgeStatus.Pending,
            ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber)
                ? null
                : request.ReferenceNumber.Trim(),
            RequestedAt = DateTimeOffset.UtcNow,
        };

        dbContext.TenantVerificationBadges.Add(badge);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(Map(badge));
    }

    public async Task<IReadOnlyList<PendingBadgeRequestResponse>> ListPendingRequestsAsync(
        CancellationToken cancellationToken = default) =>
        await (
            from badge in dbContext.TenantVerificationBadges.AsNoTracking()
            join tenant in dbContext.Tenants.AsNoTracking() on badge.TenantId equals tenant.Id
            where badge.Status == VerificationBadgeStatus.Pending
            orderby badge.RequestedAt
            select new PendingBadgeRequestResponse(
                badge.Id,
                badge.TenantId,
                tenant.Name,
                ToApiBadgeType(badge.BadgeType),
                badge.ReferenceNumber,
                badge.RequestedAt))
            .ToListAsync(cancellationToken);

    public async Task<Result<Unit>> GrantBadgeAsync(
        Guid tenantId,
        VerificationBadgeType badgeType,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var badge = await dbContext.TenantVerificationBadges
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.BadgeType == badgeType, cancellationToken);

        if (badge is null)
        {
            badge = new TenantVerificationBadge
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BadgeType = badgeType,
                RequestedAt = DateTimeOffset.UtcNow,
            };
            dbContext.TenantVerificationBadges.Add(badge);
        }

        badge.Status = VerificationBadgeStatus.Granted;
        badge.GrantedAt = DateTimeOffset.UtcNow;
        badge.GrantedByAdminId = adminId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }

    public async Task<Result<Unit>> RevokeBadgeAsync(
        Guid tenantId,
        VerificationBadgeType badgeType,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var badge = await dbContext.TenantVerificationBadges
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.BadgeType == badgeType, cancellationToken);

        if (badge is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Badge"));
        }

        badge.Status = VerificationBadgeStatus.Revoked;
        badge.GrantedAt = null;
        badge.GrantedByAdminId = adminId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }

    public async Task GrantInitialBadgesOnApprovalAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.BusinessProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(profile?.Phone))
        {
            await UpsertGrantedBadgeAsync(tenantId, VerificationBadgeType.Phone, "system", cancellationToken);
        }

        var documents = await dbContext.VerificationDocuments
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (documents.Any(x => x.DocumentType == VerificationDocumentType.Cac))
        {
            await UpsertGrantedBadgeAsync(tenantId, VerificationBadgeType.Cac, "system", cancellationToken);
        }

        if (documents.Any(x => x.DocumentType == VerificationDocumentType.AddressProof))
        {
            await UpsertGrantedBadgeAsync(tenantId, VerificationBadgeType.Address, "system", cancellationToken);
        }

        if (documents.Any(x => x.DocumentType == VerificationDocumentType.TradeLicense))
        {
            await UpsertGrantedBadgeAsync(tenantId, VerificationBadgeType.License, "system", cancellationToken);
        }
    }

    private async Task UpsertGrantedBadgeAsync(
        Guid tenantId,
        VerificationBadgeType badgeType,
        string adminId,
        CancellationToken cancellationToken)
    {
        var badge = await dbContext.TenantVerificationBadges
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.BadgeType == badgeType, cancellationToken);

        if (badge is null)
        {
            badge = new TenantVerificationBadge
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BadgeType = badgeType,
                RequestedAt = DateTimeOffset.UtcNow,
            };
            dbContext.TenantVerificationBadges.Add(badge);
        }

        badge.Status = VerificationBadgeStatus.Granted;
        badge.GrantedAt = DateTimeOffset.UtcNow;
        badge.GrantedByAdminId = adminId;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> HasBusinessAccessAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken) =>
        await dbContext.BusinessUsers
            .AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Auth0Sub == auth0Sub, cancellationToken);

    private static VerificationBadgeResponse Map(TenantVerificationBadge badge) =>
        new(
            ToApiBadgeType(badge.BadgeType),
            badge.Status.ToString().ToLowerInvariant(),
            badge.GrantedAt);

    internal static string ToApiBadgeType(VerificationBadgeType badgeType) =>
        badgeType switch
        {
            VerificationBadgeType.Phone => "phone",
            VerificationBadgeType.Cac => "cac",
            VerificationBadgeType.Address => "address",
            VerificationBadgeType.License => "license",
            _ => badgeType.ToString().ToLowerInvariant(),
        };

    public static VerificationBadgeType FromApiBadgeType(string value) =>
        value.ToLowerInvariant() switch
        {
            "phone" => VerificationBadgeType.Phone,
            "cac" => VerificationBadgeType.Cac,
            "address" => VerificationBadgeType.Address,
            "license" => VerificationBadgeType.License,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown badge type."),
        };
}
