namespace Adeni.Application.Booking;

public static class TenantBookingTabs
{
    public const string Pending = "pending";
    public const string Upcoming = "upcoming";
    public const string Past = "past";

    /// <summary>Integration tests only — not exposed on public API.</summary>
    public const string All = "all";

    public static bool IsValid(string? tab) =>
        tab is Pending or Upcoming or Past or All;
}
