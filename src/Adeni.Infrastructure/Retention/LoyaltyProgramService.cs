namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Retention;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class LoyaltyProgramService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    TimeProvider clock,
    IAuditLogWriter? audit = null,
    ICorrelationContext? correlation = null) : ILoyaltyProgramService
{
    private static readonly HashSet<string> Currencies = ["NGN", "CAD", "USD"];

    public async Task<Result<LoyaltyProgramResponse>> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureLoyaltyBasicEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<LoyaltyProgramResponse>(entitled.Error);
        }

        var program = await db.Set<LoyaltyProgram>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        return Result.Success(program is null ? LoyaltyMapper.Defaults() : LoyaltyMapper.ToResponse(program));
    }

    public async Task<Result<LoyaltyProgramResponse>> UpsertAsync(
        Guid tenantId, RetentionActor actor, UpsertLoyaltyProgramRequest request, CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureLoyaltyBasicEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<LoyaltyProgramResponse>(entitled.Error);
        }

        // The program defines what a point is worth, so only the owner may change it.
        if (!actor.IsOwner)
        {
            return Result.Failure<LoyaltyProgramResponse>(RetentionErrorCodes.OwnerRequiredError());
        }

        var invalid = Validate(request);
        if (invalid is not null)
        {
            return Result.Failure<LoyaltyProgramResponse>(invalid);
        }

        var saved = await SaveAsync(tenantId, actor, request, cancellationToken);
        if (saved.IsFailure)
        {
            return Result.Failure<LoyaltyProgramResponse>(saved.Error);
        }

        await AuditAsync(actor.Id, saved.Value!.Id, tenantId, cancellationToken);
        return Result.Success(LoyaltyMapper.ToResponse(saved.Value));
    }

    private async Task<Result<LoyaltyProgram>> SaveAsync(
        Guid tenantId, RetentionActor actor, UpsertLoyaltyProgramRequest request, CancellationToken cancellationToken)
    {
        // Two attempts: the second covers losing the create race on the one-program-per-tenant unique index.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var now = clock.GetUtcNow();
            var program = await db.Set<LoyaltyProgram>().FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
            if (program is null)
            {
                program = new LoyaltyProgram { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = now };
                db.Add(program);
            }

            program.IsEnabled = request.IsEnabled;
            program.Currency = request.Currency.Trim().ToUpperInvariant();
            program.EarnPoints = request.EarnPoints;
            program.EarnPerAmount = request.EarnPerAmount;
            program.RedeemPoints = request.RedeemPoints;
            program.RedeemValue = request.RedeemValue;
            program.MinRedeemPoints = request.MinRedeemPoints;
            program.PointsExpiryMonths = request.PointsExpiryMonths;
            program.UpdatedBy = actor.Id;
            program.UpdatedAt = now;

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return Result.Success(program);
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                db.ChangeTracker.Clear();
            }
        }

        return Result.Failure<LoyaltyProgram>(RetentionErrorCodes.LoyaltyConflictError());
    }

    private static Error? Validate(UpsertLoyaltyProgramRequest request)
    {
        var currency = request.Currency?.Trim().ToUpperInvariant();
        if (currency is null || !Currencies.Contains(currency))
        {
            return RetentionErrorCodes.ValidationError("Unsupported currency.");
        }

        if (request.EarnPoints is < 1 or > LoyaltyProgram.MaxEarnPoints)
        {
            return RetentionErrorCodes.ValidationError($"Earn points must be between 1 and {LoyaltyProgram.MaxEarnPoints}.");
        }

        if (!IsMoney(request.EarnPerAmount))
        {
            return RetentionErrorCodes.ValidationError("Earn amount must be positive with at most 2 decimals.");
        }

        if (request.RedeemPoints is < 1 or > LoyaltyProgram.MaxRedeemPoints)
        {
            return RetentionErrorCodes.ValidationError($"Redeem points must be between 1 and {LoyaltyProgram.MaxRedeemPoints:N0}.");
        }

        if (!IsMoney(request.RedeemValue))
        {
            return RetentionErrorCodes.ValidationError("Redeem value must be positive with at most 2 decimals.");
        }

        if (request.MinRedeemPoints is < 0 or > LoyaltyProgram.MaxRedeemPoints)
        {
            return RetentionErrorCodes.ValidationError($"Minimum redeem must be between 0 and {LoyaltyProgram.MaxRedeemPoints:N0} points.");
        }

        if (request.PointsExpiryMonths is { } months && (months < 1 || months > LoyaltyProgram.MaxExpiryMonths))
        {
            return RetentionErrorCodes.ValidationError($"Expiry must be between 1 and {LoyaltyProgram.MaxExpiryMonths} months, or never.");
        }

        // Loss guard: one point must never be redeemable for more than the spend it took to earn it (> 100% back).
        if (request.RedeemValue * request.EarnPoints > request.EarnPerAmount * request.RedeemPoints)
        {
            return RetentionErrorCodes.ValidationError("Redeeming points cannot be worth more than the spend that earned them.");
        }

        return null;
    }

    private static bool IsMoney(decimal value) =>
        value > 0 && value <= LoyaltyProgram.MaxAmount && decimal.Round(value, 2) == value;

    private Task AuditAsync(string actorId, Guid programId, Guid tenantId, CancellationToken cancellationToken) =>
        audit?.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actorId,
                AuditActions.RetentionLoyaltyProgramSaved,
                "retention.loyalty_program",
                programId.ToString(),
                correlation?.CorrelationId ?? "background",
                clock.GetUtcNow(),
                $"{{\"tenantId\":\"{tenantId}\"}}"),
            cancellationToken) ?? Task.CompletedTask;
}

internal static class LoyaltyMapper
{
    /// <summary>Suggested starting point for the portal form: 1 pt per 100 spent, 100 pts = 500 off (5% back).</summary>
    public static LoyaltyProgramResponse Defaults() =>
        new(false, null, false, "NGN", 1, 100m, 100, 500m, 100, 12, null, null);

    public static LoyaltyProgramResponse ToResponse(LoyaltyProgram program) =>
        new(
            true,
            program.Id,
            program.IsEnabled,
            program.Currency,
            program.EarnPoints,
            program.EarnPerAmount,
            program.RedeemPoints,
            program.RedeemValue,
            program.MinRedeemPoints,
            program.PointsExpiryMonths,
            program.CreatedAt,
            program.UpdatedAt);

    public static LoyaltyAccountResponse ToResponse(LoyaltyAccount account) =>
        new(
            account.TenantCustomerId,
            account.PointsBalance,
            account.LifetimeEarned,
            account.LifetimeRedeemed,
            account.CurrentTierId,
            account.UpdatedAt);

    public static LoyaltyAccountResponse Empty(Guid tenantCustomerId) => new(tenantCustomerId, 0, 0, 0, null, null);

    public static LoyaltyTransactionResponse ToResponse(LoyaltyTransaction row) =>
        new(row.Id, row.Sequence, ToApi(row.Kind), row.Points, row.BalanceAfter, row.Reason, row.BookingId, row.OccurredAt);

    public static string ToApi(LoyaltyTransactionKind kind) =>
        kind switch
        {
            LoyaltyTransactionKind.Earn => LoyaltyCodes.Earn,
            LoyaltyTransactionKind.Redeem => LoyaltyCodes.Redeem,
            LoyaltyTransactionKind.Expire => LoyaltyCodes.Expire,
            _ => LoyaltyCodes.Adjust,
        };
}
