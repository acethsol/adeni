namespace Adeni.Application.Booking;

using Adeni.Domain.Booking;
using Adeni.Domain.Tenancy;

public static class WalkInQueuePolicy
{
    public static bool IsAcceptingWalkIns(BusinessProfile profile, DateTimeOffset now)
    {
        if (!profile.WalkInsEnabled)
        {
            return false;
        }

        if (profile.WalkInAcceptingUntil is { } until && until <= now)
        {
            return false;
        }

        return true;
    }

    public static string ToApiPolicy(WalkInCalendarPolicy policy) =>
        policy == WalkInCalendarPolicy.Strict ? "strict" : "relaxed";

    public static WalkInCalendarPolicy FromApiPolicy(string? value) =>
        string.Equals(value, "strict", StringComparison.OrdinalIgnoreCase)
            ? WalkInCalendarPolicy.Strict
            : WalkInCalendarPolicy.Relaxed;

    public static string ToApiSource(QueueTicketSource source) =>
        source == QueueTicketSource.Appointment ? "appointment" : "walk_in";
}
