namespace Adeni.Domain.Identity;

public static class VerifiedPresenceVerificationMethods
{
    public const string Geofence = "geofence";
    public const string AttendanceClockIn = "attendance_clock_in";
    public const string ManualVerify = "manual_verify";
    public const string ManagerOverride = "manager_override";
}

public static class VerifiedPresenceInvalidationReasons
{
    public const string ClockOut = "clock_out";
    public const string Logout = "logout";
    public const string LocationSwitch = "location_switch";
    public const string Expired = "expired";
    public const string AdminInvalidate = "admin_invalidate";
    public const string Replaced = "replaced";
}
