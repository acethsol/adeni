namespace Adeni.Application.Booking;

using Adeni.Domain.Booking;
using Adeni.Domain.Common;

/// <summary>Deterministic server-side geofence evaluation for attendance clock-in (Phase B).</summary>
public static class StaffAttendanceGeofenceEvaluator
{
    public const int MinRadiusMeters = 50;
    public const int MaxRadiusMeters = 250;
    public const int DefaultRadiusMeters = 100;
    public const int MaxReportedAccuracyMeters = 100;
    public const int MaxFixAgeSeconds = 90;

    public sealed record LocationFix(double Latitude, double Longitude, int AccuracyMeters, DateTimeOffset CapturedAt);

    public sealed record Evaluation(
        string LocationVerificationStatus,
        string VerificationMethod,
        int? DistanceFromLocationMeters,
        int? ReportedAccuracyMeters,
        string? ErrorCode);

    public static Evaluation EvaluateForClockIn(
        bool geofenceEnabledOnLocation,
        bool geofenceEntitled,
        double? locationLatitude,
        double? locationLongitude,
        int configuredRadiusMeters,
        LocationFix? fix,
        DateTimeOffset serverNow)
    {
        if (!geofenceEnabledOnLocation || !geofenceEntitled)
        {
            return new Evaluation(
                StaffLocationVerificationStatuses.NotRequired,
                StaffAttendanceVerificationMethods.None,
                null,
                null,
                null);
        }

        if (locationLatitude is null or < -90 or > 90 || locationLongitude is null or < -180 or > 180)
        {
            return Fail(null, null, ErrorCodes.AttendanceLocationNotConfigured);
        }

        if (fix is null)
        {
            return Fail(null, null, ErrorCodes.StaffLocationVerificationFailed);
        }

        if (fix.Latitude is < -90 or > 90 || fix.Longitude is < -180 or > 180)
        {
            return Fail(null, fix.AccuracyMeters, ErrorCodes.StaffLocationVerificationFailed);
        }

        if (fix.AccuracyMeters is < 0 or > MaxReportedAccuracyMeters)
        {
            return Fail(null, fix.AccuracyMeters, ErrorCodes.StaffLocationVerificationFailed);
        }

        var ageSeconds = Math.Abs((serverNow - fix.CapturedAt).TotalSeconds);
        if (ageSeconds > MaxFixAgeSeconds)
        {
            return Fail(null, fix.AccuracyMeters, ErrorCodes.StaffLocationVerificationFailed);
        }

        var radius = ClampRadius(configuredRadiusMeters);
        var distanceMeters = (int)Math.Round(HaversineMeters(
            locationLatitude.Value,
            locationLongitude.Value,
            fix.Latitude,
            fix.Longitude));

        if (distanceMeters <= radius + fix.AccuracyMeters)
        {
            return new Evaluation(
                StaffLocationVerificationStatuses.Verified,
                StaffAttendanceVerificationMethods.Geofence,
                distanceMeters,
                fix.AccuracyMeters,
                null);
        }

        return Fail(distanceMeters, fix.AccuracyMeters, ErrorCodes.StaffLocationVerificationFailed);
    }

    public static int ClampRadius(int radiusMeters) =>
        Math.Clamp(radiusMeters, MinRadiusMeters, MaxRadiusMeters);

    private static Evaluation Fail(int? distanceMeters, int? accuracyMeters, string errorCode) =>
        new(
            StaffLocationVerificationStatuses.Failed,
            StaffAttendanceVerificationMethods.Geofence,
            distanceMeters,
            accuracyMeters,
            errorCode);

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusM = 6371000.0;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusM * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
