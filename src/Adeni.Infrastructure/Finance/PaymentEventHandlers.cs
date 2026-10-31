namespace Adeni.Infrastructure.Finance;

using Adeni.Application.Events;
using Adeni.Application.Finance;
using Adeni.Application.Retention;
using Adeni.Domain.Booking;
using Adeni.Domain.Booking.Events;
using Adeni.Domain.Finance;
using Adeni.Domain.Finance.Events;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class PaymentBookingHandler(
    AdeniDbContext dbContext,
    IDomainEventCollector domainEventCollector,
    IBillingDocumentService billingDocuments,
    IStaffCompensationService staffCompensation,
    IBookingDepositCalculator bookingDepositCalculator,
    ILoyaltyEarnService loyaltyEarn,
    ILoyaltyRedemptionService loyaltyRedeem,
    IReferralQualificationService referralQualification,
    ILogger<PaymentBookingHandler> logger) :
    IDomainEventHandler<PaymentCompleted>,
    IDomainEventHandler<PaymentFailed>,
    IDomainEventHandler<RefundInitiated>
{
    public async Task HandleAsync(PaymentCompleted domainEvent, CancellationToken cancellationToken = default)
    {
        var receipt = await billingDocuments.IssueReceiptForPaymentAsync(
            domainEvent.PaymentIntentId,
            cancellationToken);
        if (receipt.IsFailure)
        {
            logger.LogWarning(
                "Could not issue receipt for payment {PaymentIntentId}: {Error}",
                domainEvent.PaymentIntentId,
                receipt.Error.Message);
        }

        BookingRecord? booking = null;

        if (!domainEvent.BookingId.HasValue)
        {
            await staffCompensation.RecordTipForPaymentIntentAsync(domainEvent.PaymentIntentId, cancellationToken);
            return;
        }

        booking = await dbContext.Bookings
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(
                x => x.Id == domainEvent.BookingId.Value && x.TenantId == domainEvent.TenantId,
                cancellationToken);

        if (booking is null)
        {
            await staffCompensation.RecordTipForPaymentIntentAsync(domainEvent.PaymentIntentId, cancellationToken);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        // Deposit success confirms pending bookings only when required deposit is satisfied.
        if (domainEvent.Type == PaymentIntentType.Deposit && booking.Status == BookingStatus.Pending)
        {
            var requiredDeposit = await bookingDepositCalculator.CalculateRequiredDepositAsync(
                booking.Id,
                cancellationToken);
            if (requiredDeposit.IsFailure)
            {
                logger.LogWarning(
                    "Could not evaluate deposit requirement for booking {BookingId}: {Error}",
                    booking.Id,
                    requiredDeposit.Error.Message);
            }
            else
            {
                var completedDeposits = await dbContext.PaymentIntents
                    .AsNoTracking()
                    .Where(x =>
                        x.BookingId == booking.Id
                        && x.TenantId == booking.TenantId
                        && x.Type == PaymentIntentType.Deposit
                        && x.Status == PaymentIntentStatus.Completed)
                    .SumAsync(x => x.Amount, cancellationToken);

                if (completedDeposits >= requiredDeposit.Value!.Amount)
                {
                    booking.Status = BookingStatus.Confirmed;
                    booking.UpdatedAt = now;

                    domainEventCollector.Add(new BookingConfirmed(
                        booking.Id,
                        booking.TenantId,
                        booking.CustomerId,
                        booking.StartAt,
                        now));
                }
                else
                {
                    logger.LogWarning(
                        "Deposit payment {PaymentIntentId} for booking {BookingId} is below required deposit ({Paid} < {Required}).",
                        domainEvent.PaymentIntentId,
                        booking.Id,
                        completedDeposits,
                        requiredDeposit.Value.Amount);
                }
            }
        }

        // Balance / any booking-linked payment may close the floor loop.
        if (booking.Status is BookingStatus.Completed or BookingStatus.CheckedIn or BookingStatus.Confirmed)
        {
            var total = booking.Lines.Sum(x => x.PriceAmount);
            if (total <= 0)
            {
                var servicePrice = await dbContext.ServiceOfferings
                    .AsNoTracking()
                    .Where(x => x.Id == booking.ServiceOfferingId)
                    .Select(x => (decimal?)x.PriceAmount)
                    .FirstOrDefaultAsync(cancellationToken);
                total = servicePrice ?? 0m;
            }

            var paid = await dbContext.PaymentIntents
                .AsNoTracking()
                .Where(x =>
                    x.BookingId == booking.Id
                    && x.TenantId == booking.TenantId
                    && x.Status == PaymentIntentStatus.Completed)
                .SumAsync(x => x.Amount, cancellationToken);

            if (total > 0 && paid >= total)
            {
                booking.Status = BookingStatus.Paid;
                booking.SettlementSource = BookingSettlementSource.Online;
                booking.PaidAt = now;
                booking.UpdatedAt = now;
            }
        }

        if (booking is not null && booking.Status == BookingStatus.Paid)
        {
            var staged = await loyaltyEarn.StageEarnForPaidBookingAsync(
                domainEvent.TenantId, booking.Id, "payment:completed", cancellationToken);
            if (staged.IsFailure)
            {
                logger.LogWarning(
                    "Loyalty earn staging failed for booking {BookingId}: {Error}",
                    booking.Id,
                    staged.Error.Message);
            }

            var referral = await referralQualification.TryQualifyAndIssueRewardsForPaidBookingAsync(
                domainEvent.TenantId, booking.Id, "payment:completed", cancellationToken);
            if (referral.IsFailure)
            {
                logger.LogWarning(
                    "Referral qualification failed for booking {BookingId}: {Error}",
                    booking.Id,
                    referral.Error!.Message);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await staffCompensation.RecordTipForPaymentIntentAsync(domainEvent.PaymentIntentId, cancellationToken);

        if (booking is not null && booking.Status == BookingStatus.Paid)
        {
            await staffCompensation.AccrueCommissionForPaidBookingAsync(
                domainEvent.TenantId,
                booking.Id,
                cancellationToken);
        }
    }

    public Task HandleAsync(PaymentFailed domainEvent, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async Task HandleAsync(RefundInitiated domainEvent, CancellationToken cancellationToken = default)
    {
        if (!domainEvent.BookingId.HasValue)
        {
            return;
        }

        var booking = await dbContext.Bookings
            .FirstOrDefaultAsync(
                x => x.Id == domainEvent.BookingId.Value && x.TenantId == domainEvent.TenantId,
                cancellationToken);

        if (booking is null || booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            return;
        }

        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        domainEventCollector.Add(new BookingCancelled(
            booking.Id,
            booking.TenantId,
            booking.CustomerId,
            booking.StartAt,
            DateTimeOffset.UtcNow));

        var reversed = await loyaltyEarn.StageReverseEarnForBookingAsync(
            domainEvent.TenantId, booking.Id, "payment:refund", cancellationToken);
        if (reversed.IsFailure)
        {
            logger.LogWarning(
                "Loyalty earn reversal failed for booking {BookingId}: {Error}",
                booking.Id,
                reversed.Error.Message);
        }

        var releasedRedeem = await loyaltyRedeem.ReleaseRedeemForBookingAsync(
            domainEvent.TenantId, booking.Id, "payment:refund", cancellationToken);
        if (releasedRedeem.IsFailure)
        {
            logger.LogWarning(
                "Loyalty redeem release failed for booking {BookingId}: {Error}",
                booking.Id,
                releasedRedeem.Error.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

