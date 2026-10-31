namespace Adeni.Application.Finance;

using Adeni.Domain.Common;

public sealed record RequiredBookingDeposit(decimal Amount, string Currency);

public interface IBookingDepositCalculator
{
    Task<Result<RequiredBookingDeposit>> CalculateRequiredDepositAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);
}
