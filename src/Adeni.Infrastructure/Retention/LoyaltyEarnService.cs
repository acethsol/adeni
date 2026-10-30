namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Events;
using Adeni.Application.Retention;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Finance;
using Adeni.Domain.Retention;
using Adeni.Domain.Retention.Events;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class LoyaltyEarnService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    IBenefitRedemptionService benefits,
    ILoyaltyRedemptionService loyaltyRedeem,
    IDomainEventCollector domainEvents,
    TimeProvider clock) : ILoyaltyEarnService
{
    public async Task<Result<int?>> StageEarnForPaidBookingAsync(
        Guid tenantId,
        Guid bookingId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureLoyaltyBasicEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<int?>(entitled.Error);
        }

        var booking = await db.Bookings
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == bookingId, cancellationToken);
        if (booking is null || booking.Status != BookingStatus.Paid)
        {
            return Result.Success<int?>(null);
        }

        if (!await IsScheduledAppointmentTenantAsync(tenantId, cancellationToken))
        {
            return Result.Success<int?>(null);
        }

        var program = await db.Set<LoyaltyProgram>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (program is null || !program.IsEnabled || program.EarnPoints <= 0 || program.EarnPerAmount <= 0)
        {
            return Result.Success<int?>(null);
        }

        var existing = await db.Set<LoyaltyTransaction>()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.BookingId == bookingId
                && x.Kind == LoyaltyTransactionKind.Earn
                && x.Points > 0)
            .Select(x => (int?)x.Points)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is > 0)
        {
            return Result.Success(existing);
        }

        var staged = db.ChangeTracker.Entries<LoyaltyTransaction>()
            .Select(e => e.Entity)
            .FirstOrDefault(x =>
                x.TenantId == tenantId
                && x.BookingId == bookingId
                && x.Kind == LoyaltyTransactionKind.Earn
                && x.Points > 0);
        if (staged is not null)
        {
            return Result.Success<int?>(staged.Points);
        }

        var tenantCustomerId = await db.TenantCustomers.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerId == booking.CustomerId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (tenantCustomerId is null)
        {
            return Result.Success<int?>(null);
        }

        var currency = await ResolveBookingCurrencyAsync(booking, cancellationToken);
        if (!string.Equals(currency, program.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success<int?>(null);
        }

        var qualifyingSpend = await QualifyingSpendAsync(booking, cancellationToken);
        if (qualifyingSpend <= 0)
        {
            return Result.Success<int?>(null);
        }

        var points = (int)Math.Floor(qualifyingSpend / program.EarnPerAmount * program.EarnPoints);
        if (points <= 0)
        {
            return Result.Success<int?>(null);
        }

        var now = clock.GetUtcNow();
        var account = await db.Set<LoyaltyAccount>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId.Value, cancellationToken);
        if (account is null)
        {
            account = LoyaltyAccount.Open(tenantId, tenantCustomerId.Value, now);
            db.Add(account);
        }

        var earned = account.RecordEarn(points, bookingId, actor, now);
        if (earned.IsFailure)
        {
            return Result.Failure<int?>(earned.Error);
        }

        db.Add(earned.Value!);
        domainEvents.Add(new LoyaltyPointsEarned(
            tenantId,
            tenantCustomerId.Value,
            bookingId,
            points,
            account.PointsBalance,
            now));

        return Result.Success<int?>(points);
    }

    public async Task<Result> StageReverseEarnForBookingAsync(
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

        var earnRow = await db.Set<LoyaltyTransaction>()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.BookingId == bookingId
                && x.Kind == LoyaltyTransactionKind.Earn
                && x.Points > 0)
            .OrderBy(x => x.Sequence)
            .FirstOrDefaultAsync(cancellationToken);
        if (earnRow is null)
        {
            return Result.Success();
        }

        var alreadyReversed = await db.Set<LoyaltyTransaction>().AsNoTracking()
            .AnyAsync(x =>
                x.TenantId == tenantId
                && x.BookingId == bookingId
                && x.Kind == LoyaltyTransactionKind.Earn
                && x.Points < 0,
                cancellationToken);
        if (alreadyReversed)
        {
            return Result.Success();
        }

        var account = await db.Set<LoyaltyAccount>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == earnRow.LoyaltyAccountId, cancellationToken);
        if (account is null)
        {
            return Result.Success();
        }

        var now = clock.GetUtcNow();
        var reversal = account.RecordEarnReversal(earnRow.Points, bookingId, actor, now);
        if (reversal.IsFailure)
        {
            return Result.Failure(reversal.Error);
        }

        db.Add(reversal.Value!);
        return Result.Success();
    }

    private async Task<bool> IsScheduledAppointmentTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await db.BusinessProfiles.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.BusinessType)
            .FirstOrDefaultAsync(cancellationToken) == BusinessType.ScheduledAppointment;

    private async Task<decimal> QualifyingSpendAsync(BookingRecord booking, CancellationToken cancellationToken)
    {
        var lineTotal = booking.Lines.Sum(x => x.PriceAmount);
        if (lineTotal <= 0)
        {
            var servicePrice = await db.ServiceOfferings.AsNoTracking()
                .Where(x => x.Id == booking.ServiceOfferingId)
                .Select(x => (decimal?)x.PriceAmount)
                .FirstOrDefaultAsync(cancellationToken);
            lineTotal = servicePrice ?? 0m;
        }

        var covered = await benefits.GetCoveredServicesAsync(booking.TenantId, booking.Id, cancellationToken);
        var coveredAmount = 0m;
        foreach (var item in covered)
        {
            coveredAmount += booking.Lines
                .Where(x => x.ServiceOfferingId == item.ServiceOfferingId)
                .OrderBy(x => x.SortOrder)
                .Take(item.Quantity)
                .Sum(x => x.PriceAmount);
        }

        var qualifying = Math.Max(0m, lineTotal - coveredAmount);
        var loyaltyDiscount = await loyaltyRedeem.GetDiscountAmountForBookingAsync(
            booking.TenantId, booking.Id, cancellationToken);
        return Math.Max(0m, qualifying - loyaltyDiscount);
    }

    private async Task<string> ResolveBookingCurrencyAsync(BookingRecord booking, CancellationToken cancellationToken) =>
        await db.ServiceOfferings.AsNoTracking()
            .Where(x => x.Id == booking.ServiceOfferingId)
            .Select(x => x.Currency)
            .FirstOrDefaultAsync(cancellationToken) ?? "NGN";
}
