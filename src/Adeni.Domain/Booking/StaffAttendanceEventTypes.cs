namespace Adeni.Domain.Booking;

public static class StaffAttendanceEventTypes
{
    public const string ClockIn = "clock_in";
    public const string BreakStart = "break_start";
    public const string BreakEnd = "break_end";
    public const string ClockOut = "clock_out";
    public const string Correction = "correction";
    public const string Void = "void";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        ClockIn,
        BreakStart,
        BreakEnd,
        ClockOut,
        Correction,
        Void,
    };
}

public static class StaffAttendanceWorkStates
{
    public const string NotWorking = "not_working";
    public const string Working = "working";
    public const string OnBreak = "on_break";
}

public static class StaffLocationVerificationStatuses
{
    public const string NotRequired = "not_required";
    public const string Verified = "verified";
    public const string Failed = "failed";
    public const string Override = "override";
    public const string SkippedUnavailable = "skipped_unavailable";
}

public static class StaffAttendanceVerificationMethods
{
    public const string None = "none";
    public const string Geofence = "geofence";
    public const string Override = "override";
}
