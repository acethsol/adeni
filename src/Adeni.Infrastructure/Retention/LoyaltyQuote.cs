namespace Adeni.Infrastructure.Retention;

using Adeni.Domain.Common;
using Adeni.Domain.Retention;

internal static class LoyaltyQuote
{
    public static Result<(int Points, decimal Discount)> Compute(
        LoyaltyProgram program,
        int requestedPoints,
        int balance,
        decimal maxDiscount)
    {
        if (!program.IsEnabled)
        {
            return Result.Failure<(int, decimal)>(RetentionErrorCodes.LoyaltyProgramDisabledError());
        }

        if (requestedPoints <= 0)
        {
            return Result.Success((0, 0m));
        }

        if (requestedPoints > balance)
        {
            return Result.Failure<(int, decimal)>(RetentionErrorCodes.LoyaltyInsufficientPointsError());
        }

        if (program.RedeemPoints <= 0 || program.RedeemValue <= 0)
        {
            return Result.Failure<(int, decimal)>(RetentionErrorCodes.LoyaltyRedeemInvalidError());
        }

        if (requestedPoints < program.MinRedeemPoints)
        {
            return Result.Failure<(int, decimal)>(
                RetentionErrorCodes.LoyaltyRedeemInvalidError(
                    $"Redeem at least {program.MinRedeemPoints} points."));
        }

        if (requestedPoints % program.RedeemPoints != 0)
        {
            return Result.Failure<(int, decimal)>(
                RetentionErrorCodes.LoyaltyRedeemInvalidError(
                    $"Redeem points in multiples of {program.RedeemPoints}."));
        }

        if (maxDiscount <= 0)
        {
            return Result.Failure<(int, decimal)>(
                RetentionErrorCodes.LoyaltyRedeemInvalidError("Nothing left to discount on this booking."));
        }

        var blocks = requestedPoints / program.RedeemPoints;
        var discount = Math.Round(blocks * program.RedeemValue, 2, MidpointRounding.ToZero);
        var points = requestedPoints;

        if (discount > maxDiscount)
        {
            var maxBlocks = (int)Math.Floor(maxDiscount / program.RedeemValue);
            if (maxBlocks <= 0)
            {
                return Result.Failure<(int, decimal)>(
                    RetentionErrorCodes.LoyaltyRedeemInvalidError("The discount exceeds what is left to pay."));
            }

            points = maxBlocks * program.RedeemPoints;
            discount = Math.Round(maxBlocks * program.RedeemValue, 2, MidpointRounding.ToZero);
            discount = Math.Min(discount, maxDiscount);
        }

        return Result.Success((points, discount));
    }
}
