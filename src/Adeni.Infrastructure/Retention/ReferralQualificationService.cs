namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Events;
using Adeni.Application.Retention;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Domain.Retention.Events;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class ReferralQualificationService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    IDomainEventCollector events,
    TimeProvider clock) : IReferralQualificationService
{
    public async Task TryAttachCodeForBookingAsync(
        Guid tenantId,
        Guid platformCustomerId,
        string? referralCode,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(referralCode))
        {
            return;
        }

        if (!await entitlements.IsReferralsEntitledAsync(tenantId, cancellationToken))
        {
            return;
        }

        var program = await db.Set<ReferralProgram>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.IsEnabled, cancellationToken);
        if (program is null)
        {
            return;
        }

        var normalized = referralCode.Trim().ToUpperInvariant();
        if (normalized.Length is < ReferralCode.MinCodeLength or > ReferralCode.MaxCodeLength)
        {
            return;
        }

        var codeRow = await db.Set<ReferralCode>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == normalized, cancellationToken);
        if (codeRow is null)
        {
            return;
        }

        var referee = await db.TenantCustomers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CustomerId == platformCustomerId, cancellationToken);
        if (referee is null)
        {
            return;
        }

        if (codeRow.TenantCustomerId == referee.Id)
        {
            return;
        }

        var already = db.ChangeTracker.Entries<ReferralAttribution>()
            .Select(e => e.Entity)
            .Any(x => x.TenantId == tenantId && x.RefereeTenantCustomerId == referee.Id);
        if (!already)
        {
            already = await db.Set<ReferralAttribution>().AsNoTracking()
                .AnyAsync(x => x.TenantId == tenantId && x.RefereeTenantCustomerId == referee.Id, cancellationToken);
        }

        if (already)
        {
            return;
        }

        var now = clock.GetUtcNow();
        db.Add(new ReferralAttribution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReferrerTenantCustomerId = codeRow.TenantCustomerId,
            RefereeTenantCustomerId = referee.Id,
            ReferralCodeId = codeRow.Id,
            SourceBookingId = bookingId,
            Status = ReferralAttributionStatus.Pending,
            CreatedAt = now,
        });
    }

    public async Task<Result> TryQualifyAndIssueRewardsForPaidBookingAsync(
        Guid tenantId,
        Guid bookingId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (!await entitlements.IsReferralsEntitledAsync(tenantId, cancellationToken))
        {
            return Result.Success();
        }

        var booking = await db.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == bookingId, cancellationToken);
        if (booking is null || booking.Status is not (BookingStatus.Paid or BookingStatus.Completed))
        {
            return Result.Success();
        }

        if (!await IsScheduledAppointmentTenantAsync(tenantId, cancellationToken))
        {
            return Result.Success();
        }

        var program = await db.Set<ReferralProgram>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.IsEnabled, cancellationToken);
        if (program is null)
        {
            return Result.Success();
        }

        var loyaltyProgram = await db.Set<LoyaltyProgram>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.IsEnabled, cancellationToken);
        if (loyaltyProgram is null)
        {
            return Result.Success();
        }

        var referee = await db.TenantCustomers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CustomerId == booking.CustomerId, cancellationToken);
        if (referee is null)
        {
            return Result.Success();
        }

        var attribution = await db.Set<ReferralAttribution>()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.RefereeTenantCustomerId == referee.Id,
                cancellationToken);
        if (attribution is null)
        {
            return Result.Success();
        }

        if (attribution.Status is ReferralAttributionStatus.Rewarded or ReferralAttributionStatus.Rejected)
        {
            return Result.Success();
        }

        if (attribution.Status == ReferralAttributionStatus.Qualified
            && attribution.QualifiedBookingId == bookingId)
        {
            return await IssueRewardsAsync(program, attribution, bookingId, actor, cancellationToken);
        }

        if (!await IsFirstQualifyingVisitAsync(tenantId, booking.CustomerId, bookingId, cancellationToken))
        {
            return Result.Success();
        }

        var reject = await EvaluateFraudAsync(program, attribution, referee.Id, cancellationToken);
        if (reject is not null)
        {
            attribution.Status = ReferralAttributionStatus.Rejected;
            attribution.RejectReason = reject;
            return Result.Success();
        }

        var now = clock.GetUtcNow();
        attribution.Status = ReferralAttributionStatus.Qualified;
        attribution.QualifiedBookingId = bookingId;
        attribution.QualifiedAt = now;
        events.Add(new ReferralQualified(
            tenantId,
            attribution.Id,
            attribution.ReferrerTenantCustomerId,
            attribution.RefereeTenantCustomerId,
            bookingId,
            now));

        return await IssueRewardsAsync(program, attribution, bookingId, actor, cancellationToken);
    }

    private async Task<Result> IssueRewardsAsync(
        ReferralProgram program,
        ReferralAttribution attribution,
        Guid bookingId,
        string actor,
        CancellationToken cancellationToken)
    {
        if (attribution.Status == ReferralAttributionStatus.Rewarded)
        {
            return Result.Success();
        }

        if (attribution.Status != ReferralAttributionStatus.Qualified)
        {
            return Result.Success();
        }

        var now = clock.GetUtcNow();
        if (program.ReferrerRewardPoints > 0)
        {
            var referrer = await CreditPointsAsync(
                program.TenantId,
                attribution.ReferrerTenantCustomerId,
                program.ReferrerRewardPoints,
                bookingId,
                actor,
                "referrer",
                now,
                cancellationToken);
            if (referrer.IsFailure)
            {
                return referrer;
            }
        }

        if (program.RefereeRewardPoints > 0)
        {
            var referee = await CreditPointsAsync(
                program.TenantId,
                attribution.RefereeTenantCustomerId,
                program.RefereeRewardPoints,
                bookingId,
                actor,
                "referee",
                now,
                cancellationToken);
            if (referee.IsFailure)
            {
                return referee;
            }
        }

        attribution.Status = ReferralAttributionStatus.Rewarded;
        attribution.RewardedAt = now;
        events.Add(new ReferralRewardIssued(
            program.TenantId,
            attribution.Id,
            attribution.ReferrerTenantCustomerId,
            attribution.RefereeTenantCustomerId,
            program.ReferrerRewardPoints,
            program.RefereeRewardPoints,
            now));
        return Result.Success();
    }

    private async Task<Result> CreditPointsAsync(
        Guid tenantId,
        Guid tenantCustomerId,
        int points,
        Guid bookingId,
        string actor,
        string role,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var account = await db.Set<LoyaltyAccount>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId, cancellationToken);
        if (account is null)
        {
            account = LoyaltyAccount.Open(tenantId, tenantCustomerId, now);
            db.Add(account);
        }

        var txn = account.Adjust(points, $"referral:{role}", actor, now, null, null);
        if (txn.IsFailure)
        {
            return Result.Failure(txn.Error);
        }

        txn.Value!.BookingId = bookingId;
        db.Add(txn.Value);
        return Result.Success();
    }

    private async Task<string?> EvaluateFraudAsync(
        ReferralProgram program,
        ReferralAttribution attribution,
        Guid refereeTenantCustomerId,
        CancellationToken cancellationToken)
    {
        if (attribution.ReferrerTenantCustomerId == refereeTenantCustomerId)
        {
            return "self_referral";
        }

        var ids = await db.TenantCustomers.AsNoTracking()
            .Where(x => x.TenantId == program.TenantId
                && (x.Id == attribution.ReferrerTenantCustomerId || x.Id == refereeTenantCustomerId))
            .Select(x => new { x.Id, x.CustomerId })
            .ToListAsync(cancellationToken);
        var referrer = ids.FirstOrDefault(x => x.Id == attribution.ReferrerTenantCustomerId);
        var referee = ids.FirstOrDefault(x => x.Id == refereeTenantCustomerId);
        if (referrer?.CustomerId is not null && referrer.CustomerId == referee?.CustomerId)
        {
            return "same_customer";
        }

        if (program.MaxRewardsPerReferrer is > 0)
        {
            var rewarded = await db.Set<ReferralAttribution>().AsNoTracking()
                .CountAsync(
                    x => x.TenantId == program.TenantId
                        && x.ReferrerTenantCustomerId == attribution.ReferrerTenantCustomerId
                        && x.Status == ReferralAttributionStatus.Rewarded,
                    cancellationToken);
            if (rewarded >= program.MaxRewardsPerReferrer)
            {
                return "referrer_cap";
            }
        }

        return null;
    }

    private async Task<bool> IsFirstQualifyingVisitAsync(
        Guid tenantId,
        Guid platformCustomerId,
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var prior = await db.Bookings.AsNoTracking()
            .AnyAsync(
                x => x.TenantId == tenantId
                    && x.CustomerId == platformCustomerId
                    && x.Id != bookingId
                    && (x.Status == BookingStatus.Paid || x.Status == BookingStatus.Completed),
                cancellationToken);
        return !prior;
    }

    private async Task<bool> IsScheduledAppointmentTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await db.BusinessProfiles.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.BusinessType)
            .FirstOrDefaultAsync(cancellationToken) == BusinessType.ScheduledAppointment;
}
