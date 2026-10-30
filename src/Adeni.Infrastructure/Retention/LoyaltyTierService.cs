namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Events;
using Adeni.Application.Retention;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Domain.Retention.Events;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class LoyaltyTierService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    IDomainEventCollector events,
    TimeProvider clock,
    IAuditLogWriter? audit = null,
    ICorrelationContext? correlation = null) : ILoyaltyTierService
{
    public async Task<Result<LoyaltyTierListResponse>> ListAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        var basic = await entitlements.EnsureLoyaltyBasicEntitledAsync(tenantId, cancellationToken);
        if (basic.IsFailure)
        {
            return Result.Failure<LoyaltyTierListResponse>(basic.Error);
        }

        var tiers = await entitlements.IsLoyaltyTiersEntitledAsync(tenantId, cancellationToken);
        if (!tiers)
        {
            return Result.Success(new LoyaltyTierListResponse([]));
        }

        var rows = await db.Set<LoyaltyTier>().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.MinLifetimeEarned)
            .ThenBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        return Result.Success(new LoyaltyTierListResponse(rows.Select(LoyaltyTierMapper.ToResponse).ToArray()));
    }

    public async Task<Result<LoyaltyTierListResponse>> ReplaceAsync(
        Guid tenantId,
        RetentionActor actor,
        ReplaceLoyaltyTiersRequest request,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureLoyaltyTiersEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<LoyaltyTierListResponse>(entitled.Error);
        }

        if (!actor.IsOwner)
        {
            return Result.Failure<LoyaltyTierListResponse>(RetentionErrorCodes.OwnerRequiredError());
        }

        var seen = new HashSet<int>();
        foreach (var tier in request.Tiers)
        {
            var name = tier.Name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > LoyaltyTier.MaxNameLength)
            {
                return Result.Failure<LoyaltyTierListResponse>(
                    RetentionErrorCodes.ValidationError($"Each tier needs a name of 1–{LoyaltyTier.MaxNameLength} characters."));
            }

            if (tier.MinLifetimeEarned < 0 || !seen.Add(tier.MinLifetimeEarned))
            {
                return Result.Failure<LoyaltyTierListResponse>(
                    RetentionErrorCodes.ValidationError("Tier thresholds must be unique non-negative lifetime earned values."));
            }
        }

        var existing = await db.Set<LoyaltyTier>().Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken);
        db.RemoveRange(existing);

        var now = clock.GetUtcNow();
        var created = new List<LoyaltyTier>();
        foreach (var tier in request.Tiers.OrderBy(x => x.MinLifetimeEarned))
        {
            var row = new LoyaltyTier
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = tier.Name.Trim(),
                MinLifetimeEarned = tier.MinLifetimeEarned,
                SortOrder = tier.SortOrder,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Add(row);
            created.Add(row);
        }

        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync(actor.Id, tenantId, created.Count, cancellationToken);
        return Result.Success(new LoyaltyTierListResponse(created.Select(LoyaltyTierMapper.ToResponse).ToArray()));
    }

    public async Task EvaluateAfterEarnAsync(
        Guid tenantId,
        Guid loyaltyAccountId,
        CancellationToken cancellationToken = default)
    {
        if (!await entitlements.IsLoyaltyTiersEntitledAsync(tenantId, cancellationToken))
        {
            return;
        }

        var account = db.ChangeTracker.Entries<LoyaltyAccount>()
            .Select(e => e.Entity)
            .FirstOrDefault(x => x.TenantId == tenantId && x.Id == loyaltyAccountId);
        account ??= await db.Set<LoyaltyAccount>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == loyaltyAccountId, cancellationToken);
        if (account is null)
        {
            return;
        }

        var tiers = await db.Set<LoyaltyTier>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.MinLifetimeEarned <= account.LifetimeEarned)
            .OrderByDescending(x => x.MinLifetimeEarned)
            .ThenByDescending(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        if (tiers.Count == 0)
        {
            return;
        }

        var best = tiers[0];
        if (account.CurrentTierId == best.Id)
        {
            return;
        }

        var previous = account.CurrentTierId;
        account.CurrentTierId = best.Id;
        account.UpdatedAt = clock.GetUtcNow();
        events.Add(new LoyaltyTierChanged(
            tenantId,
            account.TenantCustomerId,
            previous,
            best.Id,
            account.LifetimeEarned,
            account.UpdatedAt));
    }

    private Task AuditAsync(string actorId, Guid tenantId, int count, CancellationToken cancellationToken) =>
        audit?.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actorId,
                AuditActions.RetentionLoyaltyTiersSaved,
                "retention.loyalty_tiers",
                tenantId.ToString(),
                correlation?.CorrelationId ?? "background",
                clock.GetUtcNow(),
                $"{{\"tenantId\":\"{tenantId}\",\"tierCount\":{count}}}"),
            cancellationToken) ?? Task.CompletedTask;
}

internal static class LoyaltyTierMapper
{
    public static LoyaltyTierResponse ToResponse(LoyaltyTier tier) =>
        new(tier.Id, tier.Name, tier.MinLifetimeEarned, tier.SortOrder, tier.CreatedAt, tier.UpdatedAt);

    public static LoyaltyRewardResponse ToResponse(LoyaltyReward reward) =>
        new(
            reward.Id,
            reward.Name,
            reward.Description,
            reward.Kind switch { LoyaltyRewardKind.FreeService => LoyaltyRewardCodes.FreeService, _ => LoyaltyRewardCodes.FreeService },
            reward.PointsCost,
            reward.ServiceOfferingId,
            reward.IsActive,
            reward.SortOrder,
            reward.CreatedAt,
            reward.UpdatedAt);
}

public static class LoyaltyRewardCodes
{
    public const string FreeService = "free_service";
}
