namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Finance;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class BookingDepositCalculator(AdeniDbContext dbContext) : IBookingDepositCalculator
{
    public async Task<Result<RequiredBookingDeposit>> CalculateRequiredDepositAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var bookingRow = await (
            from booking in dbContext.Bookings.AsNoTracking().Include(b => b.Lines)
            join service in dbContext.ServiceOfferings.AsNoTracking() on booking.ServiceOfferingId equals service.Id
            join profile in dbContext.BusinessProfiles.AsNoTracking() on booking.TenantId equals profile.TenantId
            where booking.Id == bookingId
            select new { booking, service, profile })
            .FirstOrDefaultAsync(cancellationToken);

        if (bookingRow is null)
        {
            return Result.Failure<RequiredBookingDeposit>(Error.NotFound("Booking"));
        }

        if (bookingRow.profile.DepositPercent <= 0)
        {
            return Result.Failure<RequiredBookingDeposit>(ErrorCodes.PaymentDepositNotConfiguredError());
        }

        var cartTotal = bookingRow.booking.Lines.Count > 0
            ? bookingRow.booking.Lines.Sum(x => x.PriceAmount)
            : bookingRow.service.PriceAmount;
        var guestCount = Math.Max(1, bookingRow.booking.GuestCount);
        var taxableTotal = cartTotal * guestCount;

        var depositAmount = Math.Round(
            taxableTotal * bookingRow.profile.DepositPercent / 100m,
            2,
            MidpointRounding.AwayFromZero);

        return Result.Success(new RequiredBookingDeposit(depositAmount, bookingRow.service.Currency));
    }
}
