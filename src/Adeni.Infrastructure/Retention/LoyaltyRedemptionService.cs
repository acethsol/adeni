namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Retention;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class LoyaltyRedemptionService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    TimeProvider clock) : ILoyaltyRedemptionService
{
    public async Task<Result<LoyaltyRedeemQuoteResponse>> QuoteAsync(
        Guid tenantId,
        Guid platformCustomerId,
        int pointsToRedeem,
        decimal payableAfterPackageCredits,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureLoyaltyBasicEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<LoyaltyRedeemQuoteResponse>(entitled.Error);
        }

        var program = await db.Set<LoyaltyProgram>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (program is null)
        {
            return Result.Failure<LoyaltyRedeemQuoteResponse>(RetentionErrorCodes.LoyaltyProgramNotConfiguredError());
        }

        if (!string.Equals(program.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<LoyaltyRedeemQuoteResponse>(RetentionErrorCodes.LoyaltyRedeemInvalidError());
        }

        var tenantCustomerId = await ResolveTenantCustomerIdAsync(tenantId, platformCustomerId, cancellationToken);
        if (tenantCustomerId is null)
        {
            return Result.Failure<LoyaltyRedeemQuoteResponse>(Error.NotFound("Customer"));
        }

        var balance = await db.Set<LoyaltyAccount>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId.Value)
            .Select(x => (int?)x.PointsBalance)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        var computed = LoyaltyQuote.Compute(program, pointsToRedeem, balance, payableAfterPackageCredits);
        if (computed.IsFailure)
        {
            return Result.Failure<LoyaltyRedeemQuoteResponse>(computed.Error);
        }

        var (points, discount) = computed.Value!;
        return Result.Success(new LoyaltyRedeemQuoteResponse(
            points,
            discount,
            program.Currency,
            balance,
            program.RedeemPoints,
            program.RedeemValue,
            program.MinRedeemPoints));
    }

    public async Task<Result<int>> StageRedeemForBookingAsync(
        StageLoyaltyRedeemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PointsToRedeem <= 0)
        {
            return Result.Success(0);
        }

        var entitled = await entitlements.EnsureLoyaltyBasicEntitledAsync(request.TenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<int>(entitled.Error);
        }

        var existing = await db.Set<LoyaltyTransaction>()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == request.TenantId
                && x.BookingId == request.BookingId
                && x.Kind == LoyaltyTransactionKind.Redeem
                && x.Points < 0)
            .Select(x => (int?)(-x.Points))
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is > 0)
        {
            return Result.Success(existing.Value);
        }

        var staged = db.ChangeTracker.Entries<LoyaltyTransaction>()
            .Select(e => e.Entity)
            .FirstOrDefault(x =>
                x.TenantId == request.TenantId
                && x.BookingId == request.BookingId
                && x.Kind == LoyaltyTransactionKind.Redeem
                && x.Points < 0);
        if (staged is not null)
        {
            return Result.Success(-staged.Points);
        }

        var program = await db.Set<LoyaltyProgram>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == request.TenantId, cancellationToken);
        if (program is null)
        {
            return Result.Failure<int>(RetentionErrorCodes.LoyaltyProgramNotConfiguredError());
        }

        var tenantCustomerId = await ResolveTenantCustomerIdAsync(request.TenantId, request.PlatformCustomerId, cancellationToken);
        if (tenantCustomerId is null)
        {
            return Result.Failure<int>(Error.NotFound("Customer"));
        }

        var account = await db.Set<LoyaltyAccount>()
            .FirstOrDefaultAsync(x => x.TenantId == request.TenantId && x.TenantCustomerId == tenantCustomerId.Value, cancellationToken);
        var balance = account?.PointsBalance ?? 0;

        var computed = LoyaltyQuote.Compute(
            program, request.PointsToRedeem, balance, request.PayableAfterPackageCredits);
        if (computed.IsFailure)
        {
            return Result.Failure<int>(computed.Error);
        }

        var (points, discount) = computed.Value!;
        if (points <= 0)
        {
            return Result.Success(0);
        }

        var now = request.At == default ? clock.GetUtcNow() : request.At;
        if (account is null)
        {
            return Result.Failure<int>(RetentionErrorCodes.LoyaltyInsufficientPointsError());
        }

        var redeemed = account.RecordRedeem(points, discount, request.BookingId, request.Actor, now);
        if (redeemed.IsFailure)
        {
            return Result.Failure<int>(redeemed.Error);
        }

        db.Add(redeemed.Value!);
        return Result.Success(points);
    }

    public async Task<Result> ReleaseRedeemForBookingAsync(
        Guid tenantId,
        Guid bookingId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureLoyaltyBasicEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return entitled;
        }

        var redeemRow = await db.Set<LoyaltyTransaction>()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.BookingId == bookingId
                && x.Kind == LoyaltyTransactionKind.Redeem
                && x.Points < 0)
            .OrderBy(x => x.Sequence)
            .FirstOrDefaultAsync(cancellationToken);
        if (redeemRow is null)
        {
            return Result.Success();
        }

        var alreadyReleased = await db.Set<LoyaltyTransaction>().AsNoTracking()
            .AnyAsync(x =>
                x.TenantId == tenantId
                && x.BookingId == bookingId
                && x.Kind == LoyaltyTransactionKind.Redeem
                && x.Points > 0,
                cancellationToken);
        if (alreadyReleased)
        {
            return Result.Success();
        }

        var account = await db.Set<LoyaltyAccount>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == redeemRow.LoyaltyAccountId, cancellationToken);
        if (account is null)
        {
            return Result.Success();
        }

        var points = -redeemRow.Points;
        var discount = redeemRow.DiscountAmount ?? 0m;
        var now = clock.GetUtcNow();
        var reversal = account.RecordRedeemReversal(points, discount, bookingId, actor, now);
        if (reversal.IsFailure)
        {
            return Result.Failure(reversal.Error);
        }

        db.Add(reversal.Value!);
        return Result.Success();
    }

    public async Task<decimal> GetDiscountAmountForBookingAsync(
        Guid tenantId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var fromDb = await db.Set<LoyaltyTransaction>().AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.BookingId == bookingId
                && x.Kind == LoyaltyTransactionKind.Redeem
                && x.Points < 0)
            .Select(x => x.DiscountAmount)
            .FirstOrDefaultAsync(cancellationToken);
        if (fromDb is > 0)
        {
            return fromDb.Value;
        }

        return db.ChangeTracker.Entries<LoyaltyTransaction>()
            .Select(e => e.Entity)
            .Where(x =>
                x.TenantId == tenantId
                && x.BookingId == bookingId
                && x.Kind == LoyaltyTransactionKind.Redeem
                && x.Points < 0
                && x.DiscountAmount is > 0)
            .Select(x => x.DiscountAmount!.Value)
            .FirstOrDefault();
    }

    private async Task<Guid?> ResolveTenantCustomerIdAsync(
        Guid tenantId, Guid platformCustomerId, CancellationToken cancellationToken) =>
        await db.TenantCustomers.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerId == platformCustomerId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
