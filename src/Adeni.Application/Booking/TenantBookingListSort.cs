namespace Adeni.Application.Booking;

using Adeni.Application.Common;

public static class TenantBookingListSort
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "customer",
        "when",
        "service",
        "status",
    };

    public const string DefaultField = "when";

    public const ListSortDirection DefaultDirection = ListSortDirection.Asc;
}
