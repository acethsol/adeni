namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Retention;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class LoyaltyRewardCatalogService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    TimeProvider clock,
    IAuditLogWriter? audit = null,
    ICorrelationContext? correlation = null) : ILoyaltyRewardCatalogService
{
    private const string RedeemKind = "loyalty_reward_redeem";

    public async Task<Result<LoyaltyRewardListResponse>> ListAsync(
        Guid tenantId, bool activeOnly, CancellationToken cancellationToken = default)
    {
        var basic = await entitlements.EnsureLoyaltyBasicEntitledAsync(tenantId, cancellationToken);
        if (basic.IsFailure)
        {
            return Result.Failure<LoyaltyRewardListResponse>(basic.Error);
        }

        if (!await entitlements.IsLoyaltyTiersEntitledAsync(tenantId, cancellationToken))
        {
            return Result.Success(new LoyaltyRewardListResponse([]));
        }

        var query = db.Set<LoyaltyReward>().AsNoTracking().Where(x => x.TenantId == tenantId);
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        var rows = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        return Result.Success(new LoyaltyRewardListResponse(rows.Select(LoyaltyTierMapper.ToResponse).ToArray()));
    }

    public async Task<Result<LoyaltyRewardResponse>> CreateAsync(
        Guid tenantId,
        RetentionActor actor,
        CreateLoyaltyRewardRequest request,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureLoyaltyTiersEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<LoyaltyRewardResponse>(entitled.Error);
        }

        if (!actor.IsOwner)
        {
            return Result.Failure<LoyaltyRewardResponse>(RetentionErrorCodes.OwnerRequiredError());
        }

        var invalid = Validate(request.Name, request.Description, request.PointsCost, request.Kind);
        if (invalid is not null)
        {
            return Result.Failure<LoyaltyRewardResponse>(invalid);
        }

        var now = clock.GetUtcNow();
        var reward = new LoyaltyReward
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Kind = ParseKind(request.Kind),
            PointsCost = request.PointsCost,
            ServiceOfferingId = request.ServiceOfferingId,
            IsActive = true,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Add(reward);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(LoyaltyTierMapper.ToResponse(reward));
    }

    public async Task<Result<LoyaltyRewardResponse>> UpdateAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid rewardId,
        UpdateLoyaltyRewardRequest request,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureLoyaltyTiersEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<LoyaltyRewardResponse>(entitled.Error);
        }

        if (!actor.IsOwner)
        {
            return Result.Failure<LoyaltyRewardResponse>(RetentionErrorCodes.OwnerRequiredError());
        }

        var invalid = Validate(request.Name, request.Description, request.PointsCost, LoyaltyRewardCodes.FreeService);
        if (invalid is not null)
        {
            return Result.Failure<LoyaltyRewardResponse>(invalid);
        }

        var reward = await db.Set<LoyaltyReward>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == rewardId, cancellationToken);
        if (reward is null)
        {
            return Result.Failure<LoyaltyRewardResponse>(Error.NotFound("Reward"));
        }

        reward.Name = request.Name.Trim();
        reward.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        reward.PointsCost = request.PointsCost;
        reward.ServiceOfferingId = request.ServiceOfferingId;
        reward.IsActive = request.IsActive;
        reward.SortOrder = request.SortOrder;
        reward.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(LoyaltyTierMapper.ToResponse(reward));
    }

    public async Task<Result<LoyaltyRewardRedemptionResponse>> RedeemForCustomerAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid tenantCustomerId,
        Guid rewardId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureLoyaltyTiersEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<LoyaltyRewardRedemptionResponse>(entitled.Error);
        }

        if (!actor.IsOwnerOrManager)
        {
            return Result.Failure<LoyaltyRewardRedemptionResponse>(RetentionErrorCodes.OwnerRequiredError());
        }

        var key = Adeni.Application.Common.IdempotencyKeyNormalizer.Normalize(idempotencyKey);
        if (key is null)
        {
            return Result.Failure<LoyaltyRewardRedemptionResponse>(RetentionErrorCodes.IdempotencyKeyRequiredError());
        }

        var keyHash = RetentionHash.KeyHash(tenantId, RedeemKind, actor.Id, key);
        var fingerprint = RetentionHash.Fingerprint(tenantCustomerId, rewardId);
        var replay = await db.Set<LoyaltyTransaction>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.KeyHash == keyHash, cancellationToken);
        if (replay is not null)
        {
            if (replay.Fingerprint != fingerprint)
            {
                return Result.Failure<LoyaltyRewardRedemptionResponse>(RetentionErrorCodes.IdempotencyConflictError());
            }

            var replayAccount = await db.Set<LoyaltyAccount>().AsNoTracking()
                .FirstAsync(x => x.TenantId == tenantId && x.Id == replay.LoyaltyAccountId, cancellationToken);
            return Result.Success(new LoyaltyRewardRedemptionResponse(
                replay.Id,
                rewardId,
                -replay.Points,
                replay.OccurredAt,
                LoyaltyMapper.ToResponse(replayAccount),
                LoyaltyMapper.ToResponse(replay)));
        }

        var reward = await db.Set<LoyaltyReward>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == rewardId && x.IsActive, cancellationToken);
        if (reward is null)
        {
            return Result.Failure<LoyaltyRewardRedemptionResponse>(Error.NotFound("Reward"));
        }

        var exists = await db.TenantCustomers.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == tenantCustomerId, cancellationToken);
        if (!exists)
        {
            return Result.Failure<LoyaltyRewardRedemptionResponse>(Error.NotFound("Customer"));
        }

        var now = clock.GetUtcNow();
        var account = await db.Set<LoyaltyAccount>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId, cancellationToken);
        if (account is null)
        {
            return Result.Failure<LoyaltyRewardRedemptionResponse>(RetentionErrorCodes.LoyaltyInsufficientPointsError());
        }

        var redeemed = account.RecordCatalogRedeem(reward.PointsCost, rewardId, actor.Id, now);
        if (redeemed.IsFailure)
        {
            return Result.Failure<LoyaltyRewardRedemptionResponse>(redeemed.Error);
        }

        var txn = redeemed.Value!;
        txn.KeyHash = keyHash;
        txn.Fingerprint = fingerprint;
        db.Add(txn);
        db.Add(new LoyaltyRewardRedemption
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LoyaltyAccountId = account.Id,
            TenantCustomerId = tenantCustomerId,
            LoyaltyRewardId = rewardId,
            LoyaltyTransactionId = txn.Id,
            PointsSpent = reward.PointsCost,
            IssuedAt = now,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Result.Failure<LoyaltyRewardRedemptionResponse>(RetentionErrorCodes.LoyaltyConflictError());
        }

        await AuditAsync(actor.Id, tenantId, tenantCustomerId, rewardId, reward.PointsCost, cancellationToken);
        return Result.Success(new LoyaltyRewardRedemptionResponse(
            txn.Id,
            rewardId,
            reward.PointsCost,
            now,
            LoyaltyMapper.ToResponse(account),
            LoyaltyMapper.ToResponse(txn)));
    }

    private static Error? Validate(string? name, string? description, int pointsCost, string kind)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > LoyaltyReward.MaxNameLength)
        {
            return RetentionErrorCodes.ValidationError($"Reward name is required (max {LoyaltyReward.MaxNameLength} characters).");
        }

        if (description is { Length: > LoyaltyReward.MaxDescriptionLength })
        {
            return RetentionErrorCodes.ValidationError("Description is too long.");
        }

        if (pointsCost <= 0 || pointsCost > LoyaltyReward.MaxPointsCost)
        {
            return RetentionErrorCodes.ValidationError("Points cost is out of range.");
        }

        if (!string.Equals(kind, LoyaltyRewardCodes.FreeService, StringComparison.OrdinalIgnoreCase))
        {
            return RetentionErrorCodes.ValidationError("Unsupported reward kind.");
        }

        return null;
    }

    private static LoyaltyRewardKind ParseKind(string kind) =>
        string.Equals(kind, LoyaltyRewardCodes.FreeService, StringComparison.OrdinalIgnoreCase)
            ? LoyaltyRewardKind.FreeService
            : LoyaltyRewardKind.FreeService;

    private Task AuditAsync(
        string actorId, Guid tenantId, Guid tenantCustomerId, Guid rewardId, int points, CancellationToken cancellationToken) =>
        audit?.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actorId,
                AuditActions.RetentionLoyaltyRewardRedeemed,
                "retention.loyalty_reward",
                rewardId.ToString(),
                correlation?.CorrelationId ?? "background",
                clock.GetUtcNow(),
                $"{{\"tenantId\":\"{tenantId}\",\"tenantCustomerId\":\"{tenantCustomerId}\",\"points\":{points}}}"),
            cancellationToken) ?? Task.CompletedTask;
}
