namespace Adeni.Domain.Booking;

public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    Rejected = 2,
    Cancelled = 3,
    CheckedIn = 4,
    Completed = 5,
    Paid = 6,
}

/// <summary>How the booking was settled when status is <see cref="BookingStatus.Paid"/>.</summary>
public enum BookingSettlementSource
{
    None = 0,
    Online = 1,
    ExternalTill = 2,
}
