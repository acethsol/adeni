namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Events;
using Adeni.Application.Retention;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Domain.Retention.Events;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Reserve → release / finalize for booking-applied package credits. Methods stage changes on the
/// shared unit of work (same <see cref="AdeniDbContext"/> as Booking) — the caller's single SaveChanges commits atomically.
/// </summary>
public sealed class BenefitRedemptionService(
    AdeniDbContext db,
    IDomainEventCollector events,
    TimeProvider clock) : IBenefitRedemptionService
{
    public async Task<Result<IReadOnlyList<ReservedBenefit>>> TryReserveBenefitsAsync(
        ReserveBenefitsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Benefits.Count == 0)
        {
            return Result.Success<IReadOnlyList<ReservedBenefit>>([]);
        }

        if (request.Benefits.Any(x => x.Quantity <= 0))
        {
            return Failure(RetentionErrorCodes.ValidationError("Applied quantity must be positive."));
        }

        // One entry per balance (sum duplicates) so the invariant is checked once.
        var wanted = request.Benefits
            .GroupBy(x => x.BenefitBalanceId)
            .Select(g => new { BalanceId = g.Key, PackageId = g.First().CustomerPackageId, Quantity = g.Sum(x => x.Quantity), Mixed = g.Select(x => x.CustomerPackageId).Distinct().Count() > 1 })
            .ToArray();
        if (wanted.Any(x => x.Mixed))
        {
            return Failure(RetentionErrorCodes.BenefitNotFoundError());
        }

        var tenantCustomerId = await db.TenantCustomers.AsNoTracking()
            .Where(x => x.TenantId == request.TenantId && x.CustomerId == request.PlatformCustomerId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (tenantCustomerId is null)
        {
            return Failure(RetentionErrorCodes.PackageNotFoundError());
        }

        var balanceIds = wanted.Select(x => x.BalanceId).ToArray();
        var packageIds = wanted.Select(x => x.PackageId).Distinct().ToArray();
        var balances = await db.Set<BenefitBalance>()
            .Where(x => x.TenantId == request.TenantId && balanceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var packages = await db.Set<CustomerPackage>().AsNoTracking()
            .Where(x => x.TenantId == request.TenantId && x.TenantCustomerId == tenantCustomerId && packageIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var existingReserves = await db.Set<BenefitTransaction>().AsNoTracking()
            .Where(x => x.TenantId == request.TenantId && x.BookingId == request.BookingId
                && x.Kind == BenefitTransactionKind.Reserve && balanceIds.Contains(x.BenefitBalanceId))
            .Select(x => x.BenefitBalanceId)
            .ToListAsync(cancellationToken);

        // Phase 1: validate every applied benefit — nothing is mutated unless all pass (all-or-nothing).
        var plan = new List<(BenefitBalance Balance, CustomerPackage Package, int Quantity)>();
        foreach (var item in wanted)
        {
            if (!balances.TryGetValue(item.BalanceId, out var balance)
                || !packages.TryGetValue(item.PackageId, out var package)
                || balance.CustomerPackageId != package.Id)
            {
                return Failure(RetentionErrorCodes.BenefitNotFoundError());
            }

            if (existingReserves.Contains(balance.Id))
            {
                continue; // Replay of the same booking: already reserved.
            }

            if (package.Status == CustomerPackageStatus.Expired
                || (package.Status == CustomerPackageStatus.Active && !package.IsRedeemableAt(request.At))
                || (balance.ExpiresAt is { } balanceExpiry && balanceExpiry <= request.At))
            {
                return Failure(RetentionErrorCodes.PackageExpiredError());
            }

            if (package.Status != CustomerPackageStatus.Active)
            {
                return Failure(RetentionErrorCodes.PackageNotActiveError());
            }

            var matchingLines = request.BookedServiceOfferingIds.Count(x => x == balance.ServiceOfferingId);
            if (matchingLines == 0 || item.Quantity > matchingLines * Math.Max(1, request.GuestCount))
            {
                return Failure(RetentionErrorCodes.BenefitServiceMismatchError());
            }

            if (!PackageScopePolicy.CanRedeemAt(
                    package.RedemptionScopeSnapshot,
                    LocationIdList.Parse(package.RedemptionLocationIdsJson),
                    package.SoldAtLocationId,
                    request.BusinessLocationId))
            {
                return Failure(RetentionErrorCodes.ScopeLocationDeniedError());
            }

            if (balance.Remaining < item.Quantity)
            {
                return Failure(RetentionErrorCodes.BenefitInsufficientError());
            }

            plan.Add((balance, package, item.Quantity));
        }

        // Phase 2: stage counters + ledger rows (rowversion on the balance guards concurrent writers).
        var reserved = new List<ReservedBenefit>();
        foreach (var (balance, package, quantity) in plan)
        {
            var result = balance.Reserve(quantity, request.BookingId, request.BusinessLocationId, request.Actor, request.At);
            if (result.IsFailure)
            {
                return Failure(result.Error);
            }

            db.Add(result.Value!);
            events.Add(new BenefitReserved(balance.Id, request.TenantId, request.BookingId, quantity, request.At));
            reserved.Add(new ReservedBenefit(package.Id, balance.Id, balance.ServiceOfferingId, quantity));
        }
        return Result.Success<IReadOnlyList<ReservedBenefit>>(reserved);
    }

    public Task<Result> ReleaseReservationsAsync(
        Guid tenantId, Guid bookingId, string actor, string reason, CancellationToken cancellationToken = default) =>
        SettleAsync(tenantId, bookingId, actor, reason, redeem: false, cancellationToken);

    public Task<Result> FinalizeRedemptionsAsync(
        Guid tenantId, Guid bookingId, string actor, CancellationToken cancellationToken = default) =>
        SettleAsync(tenantId, bookingId, actor, null, redeem: true, cancellationToken);

    public async Task<Result<RedeemableBenefitListResponse>> ListRedeemableAsync(
        Guid tenantId,
        Guid tenantCustomerId,
        Guid? businessLocationId,
        IReadOnlyList<Guid>? serviceOfferingIds,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var packages = await db.Set<CustomerPackage>().AsNoTracking()
            .Include(x => x.Balances)
            .Where(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId
                && x.Status == CustomerPackageStatus.Active && (x.ExpiresAt == null || x.ExpiresAt > now))
            .ToListAsync(cancellationToken);

        var items = new List<RedeemableBenefitResponse>();
        foreach (var package in packages)
        {
            if (businessLocationId is not null
                && !PackageScopePolicy.CanRedeemAt(
                    package.RedemptionScopeSnapshot,
                    LocationIdList.Parse(package.RedemptionLocationIdsJson),
                    package.SoldAtLocationId,
                    businessLocationId))
            {
                continue;
            }

            foreach (var balance in package.Balances.Where(x => x.Remaining > 0
                && (x.ExpiresAt == null || x.ExpiresAt > now)
                && (serviceOfferingIds is not { Count: > 0 } || serviceOfferingIds.Contains(x.ServiceOfferingId))))
            {
                items.Add(new RedeemableBenefitResponse(
                    package.Id, package.NameSnapshot, balance.Id, balance.ServiceOfferingId,
                    balance.ServiceName, balance.Remaining, balance.ExpiresAt ?? package.ExpiresAt));
            }
        }

        return Result.Success(new RedeemableBenefitListResponse(items));
    }

    public async Task<IReadOnlyList<CoveredBenefitService>> GetCoveredServicesAsync(
        Guid tenantId, Guid bookingId, CancellationToken cancellationToken = default)
    {
        var ledger = await db.Set<BenefitTransaction>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BookingId == bookingId)
            .ToListAsync(cancellationToken);
        var covered = ledger
            .Where(r => r.Kind == BenefitTransactionKind.Reserve
                && !ledger.Any(o => o.BenefitBalanceId == r.BenefitBalanceId && o.Kind == BenefitTransactionKind.Release))
            .ToArray();
        if (covered.Length == 0)
        {
            return [];
        }

        var ids = covered.Select(x => x.BenefitBalanceId).Distinct().ToArray();
        var services = await db.Set<BenefitBalance>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.ServiceOfferingId, cancellationToken);

        return covered
            .Where(x => services.ContainsKey(x.BenefitBalanceId))
            .GroupBy(x => services[x.BenefitBalanceId])
            .Select(g => new CoveredBenefitService(g.Key, g.Sum(x => x.Quantity)))
            .ToArray();
    }

    public bool IsBenefitConflict(Exception error) =>
        error is DbUpdateException update
        && update.Entries.Any(e => e.Entity is BenefitBalance or BenefitTransaction or CustomerPackage);

    private async Task<Result> SettleAsync(
        Guid tenantId, Guid bookingId, string actor, string? reason, bool redeem, CancellationToken cancellationToken)
    {
        var ledger = await db.Set<BenefitTransaction>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BookingId == bookingId)
            .ToListAsync(cancellationToken);

        var open = ledger
            .Where(r => r.Kind == BenefitTransactionKind.Reserve
                && !ledger.Any(o => o.BenefitBalanceId == r.BenefitBalanceId
                    && o.Kind is BenefitTransactionKind.Release or BenefitTransactionKind.Redeem))
            .ToArray();
        if (open.Length == 0)
        {
            return Result.Success();
        }

        var ids = open.Select(x => x.BenefitBalanceId).ToArray();
        var balances = await db.Set<BenefitBalance>()
            .Where(x => x.TenantId == tenantId && ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var now = clock.GetUtcNow();
        foreach (var reservation in open)
        {
            if (!balances.TryGetValue(reservation.BenefitBalanceId, out var balance))
            {
                return Result.Failure(RetentionErrorCodes.BenefitNotFoundError());
            }

            var result = redeem
                ? balance.Redeem(reservation.Quantity, bookingId, reservation.BusinessLocationId, actor, now)
                : balance.Release(reservation.Quantity, bookingId, reservation.BusinessLocationId, actor, reason, now);
            if (result.IsFailure)
            {
                return Result.Failure(result.Error);
            }

            db.Add(result.Value!);
            events.Add(redeem
                ? new BenefitRedeemed(balance.Id, tenantId, bookingId, reservation.Quantity, now)
                : new BenefitReleased(balance.Id, tenantId, bookingId, reservation.Quantity, now));
        }

        return Result.Success();
    }

    private static Result<IReadOnlyList<ReservedBenefit>> Failure(Error error) =>
        Result.Failure<IReadOnlyList<ReservedBenefit>>(error);
}