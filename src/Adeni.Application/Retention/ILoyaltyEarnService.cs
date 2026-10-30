namespace Adeni.Application.Retention;

using Adeni.Domain.Common;

/// <summary>
/// Loyalty earn on paid bookings (Sprint 37.3-B). Stages ledger rows on the caller's <see cref="AdeniDbContext"/> unit
/// of work — the caller's <c>SaveChanges</c> commits atomically with booking/payment state.
/// </summary>
public interface ILoyaltyEarnService
{
    /// <summary>Idempotent per booking. Returns earned points, or null when nothing applied.</summary>
    Task<Result<int?>> StageEarnForPaidBookingAsync(
        Guid tenantId,
        Guid bookingId,
        string actor,
        CancellationToken cancellationToken = default);

    /// <summary>Reverses a prior earn when a paid booking is cancelled/refunded. Idempotent.</summary>
    Task<Result> StageReverseEarnForBookingAsync(
        Guid tenantId,
        Guid bookingId,
        string actor,
        CancellationToken cancellationToken = default);
}
