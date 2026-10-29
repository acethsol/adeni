namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Retention;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class LoyaltyAccountService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    TimeProvider clock,
    IAuditLogWriter? audit = null,
    ICorrelationContext? correlation = null) : ILoyaltyAccountService
{
    private const string AdjustKind = "loyalty_adjust";
    private const int MaxReasonLength = 500;
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    public async Task<Result<LoyaltyAccountResponse>> GetForCustomerAsync(
        Guid tenantId, Guid tenantCustomerId, CancellationToken cancellationToken = default)
    {
        var guard = await EnsureCustomerAsync(tenantId, tenantCustomerId, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<LoyaltyAccountResponse>(guard.Error);
        }

        var account = await db.Set<LoyaltyAccount>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId, cancellationToken);
        return Result.Success(account is null ? LoyaltyMapper.Empty(tenantCustomerId) : LoyaltyMapper.ToResponse(account));
    }

    public async Task<Result<LoyaltyTransactionListResponse>> ListTransactionsAsync(
        Guid tenantId, Guid tenantCustomerId, int limit, CancellationToken cancellationToken = default)
    {
        var guard = await EnsureCustomerAsync(tenantId, tenantCustomerId, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<LoyaltyTransactionListResponse>(guard.Error);
        }

        var take = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);
        var rows = await db.Set<LoyaltyTransaction>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId)
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Sequence)
            .Take(take)
            .ToListAsync(cancellationToken);
        return Result.Success(new LoyaltyTransactionListResponse(rows.Select(LoyaltyMapper.ToResponse).ToArray()));
    }

    public async Task<Result<LoyaltyAdjustmentResponse>> AdjustAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid tenantCustomerId,
        AdjustLoyaltyPointsRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureLoyaltyBasicEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<LoyaltyAdjustmentResponse>(entitled.Error);
        }

        // Parent §6.7: manual adjustments are a manager-level, audited action.
        if (!actor.IsOwnerOrManager)
        {
            return Result.Failure<LoyaltyAdjustmentResponse>(RetentionErrorCodes.OwnerRequiredError());
        }

        var key = Adeni.Application.Common.IdempotencyKeyNormalizer.Normalize(idempotencyKey);
        if (key is null)
        {
            return Result.Failure<LoyaltyAdjustmentResponse>(RetentionErrorCodes.IdempotencyKeyRequiredError());
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > MaxReasonLength)
        {
            return Result.Failure<LoyaltyAdjustmentResponse>(
                RetentionErrorCodes.ValidationError($"A reason of 1–{MaxReasonLength} characters is required."));
        }

        if (request.Points == 0 || Math.Abs((long)request.Points) > LoyaltyAccount.MaxAdjustPoints)
        {
            return Result.Failure<LoyaltyAdjustmentResponse>(
                RetentionErrorCodes.ValidationError(
                    $"Points must be a non-zero whole number up to {LoyaltyAccount.MaxAdjustPoints:N0}."));
        }

        var customer = await EnsureCustomerAsync(tenantId, tenantCustomerId, cancellationToken, skipEntitlement: true);
        if (customer.IsFailure)
        {
            return Result.Failure<LoyaltyAdjustmentResponse>(customer.Error);
        }

        var keyHash = RetentionHash.KeyHash(tenantId, AdjustKind, actor.Id, key);
        var fingerprint = RetentionHash.Fingerprint(tenantCustomerId, request.Points, reason);

        var replay = await ReplayAsync(tenantId, keyHash, fingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay.Value;
        }

        var programExists = await db.Set<LoyaltyProgram>().AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId, cancellationToken);
        if (!programExists)
        {
            return Result.Failure<LoyaltyAdjustmentResponse>(RetentionErrorCodes.LoyaltyProgramNotConfiguredError());
        }

        var now = clock.GetUtcNow();
        var account = await db.Set<LoyaltyAccount>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId, cancellationToken);
        if (account is null)
        {
            account = LoyaltyAccount.Open(tenantId, tenantCustomerId, now);
            db.Add(account);
        }

        var adjusted = account.Adjust(request.Points, reason, actor.Id, now, keyHash, fingerprint);
        if (adjusted.IsFailure)
        {
            db.ChangeTracker.Clear();
            return Result.Failure<LoyaltyAdjustmentResponse>(adjusted.Error);
        }

        var transaction = adjusted.Value!;
        db.Add(transaction);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return Result.Failure<LoyaltyAdjustmentResponse>(RetentionErrorCodes.LoyaltyConflictError());
        }
        catch (DbUpdateException)
        {
            // Lost a race on the idempotency key (replay) or on first-account creation (retryable conflict).
            db.ChangeTracker.Clear();
            var winner = await ReplayAsync(tenantId, keyHash, fingerprint, cancellationToken);
            return winner ?? Result.Failure<LoyaltyAdjustmentResponse>(RetentionErrorCodes.LoyaltyConflictError());
        }

        await AuditAsync(actor.Id, account.Id, tenantId, tenantCustomerId, request.Points, cancellationToken);
        return Result.Success(new LoyaltyAdjustmentResponse(LoyaltyMapper.ToResponse(account), LoyaltyMapper.ToResponse(transaction)));
    }

    private async Task<Result> EnsureCustomerAsync(
        Guid tenantId, Guid tenantCustomerId, CancellationToken cancellationToken, bool skipEntitlement = false)
    {
        if (!skipEntitlement)
        {
            var entitled = await entitlements.EnsureLoyaltyBasicEntitledAsync(tenantId, cancellationToken);
            if (entitled.IsFailure)
            {
                return entitled;
            }
        }

        var exists = await db.TenantCustomers.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == tenantCustomerId, cancellationToken);
        return exists ? Result.Success() : Result.Failure(Error.NotFound("Customer"));
    }

    private async Task<Result<LoyaltyAdjustmentResponse>?> ReplayAsync(
        Guid tenantId, string keyHash, string fingerprint, CancellationToken cancellationToken)
    {
        var existing = await db.Set<LoyaltyTransaction>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.KeyHash == keyHash, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (existing.Fingerprint != fingerprint)
        {
            return Result.Failure<LoyaltyAdjustmentResponse>(RetentionErrorCodes.IdempotencyConflictError());
        }

        var account = await db.Set<LoyaltyAccount>().AsNoTracking()
            .FirstAsync(x => x.TenantId == tenantId && x.Id == existing.LoyaltyAccountId, cancellationToken);
        return Result.Success(new LoyaltyAdjustmentResponse(LoyaltyMapper.ToResponse(account), LoyaltyMapper.ToResponse(existing)));
    }

    // Metadata carries ids and the delta only — never the staff-entered reason text or any customer PII.
    private Task AuditAsync(
        string actorId, Guid accountId, Guid tenantId, Guid tenantCustomerId, int points, CancellationToken cancellationToken) =>
        audit?.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actorId,
                AuditActions.RetentionLoyaltyPointsAdjusted,
                "retention.loyalty_account",
                accountId.ToString(),
                correlation?.CorrelationId ?? "background",
                clock.GetUtcNow(),
                $"{{\"tenantId\":\"{tenantId}\",\"tenantCustomerId\":\"{tenantCustomerId}\",\"points\":{points}}}"),
            cancellationToken) ?? Task.CompletedTask;
}
