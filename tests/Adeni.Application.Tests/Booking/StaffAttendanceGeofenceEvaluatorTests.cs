namespace Adeni.Application.Tests.Booking;

using Adeni.Application.Booking;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;

public sealed class StaffAttendanceGeofenceEvaluatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-15T12:00:00Z");

    [Fact]
    public void Evaluate_whenGeofenceDisabled_notRequired()
    {
        var result = StaffAttendanceGeofenceEvaluator.EvaluateForClockIn(
            geofenceEnabledOnLocation: false,
            geofenceEntitled: true,
            6.5,
            3.4,
            100,
            new StaffAttendanceGeofenceEvaluator.LocationFix(6.5, 3.4, 20, Now),
            Now);
        Assert.Equal(StaffLocationVerificationStatuses.NotRequired, result.LocationVerificationStatus);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Evaluate_acceptsWithinRadiusPlusAccuracy()
    {
        // ~111m per 0.001° latitude at equator; use same point for zero distance
        var result = StaffAttendanceGeofenceEvaluator.EvaluateForClockIn(
            true,
            true,
            6.5244,
            3.3792,
            50,
            new StaffAttendanceGeofenceEvaluator.LocationFix(6.5244, 3.3792, 40, Now),
            Now);
        Assert.Equal(StaffLocationVerificationStatuses.Verified, result.LocationVerificationStatus);
    }

    [Fact]
    public void Evaluate_rejectsStaleFix()
    {
        var result = StaffAttendanceGeofenceEvaluator.EvaluateForClockIn(
            true,
            true,
            6.5244,
            3.3792,
            100,
            new StaffAttendanceGeofenceEvaluator.LocationFix(6.5244, 3.3792, 10, Now.AddMinutes(-2)),
            Now);
        Assert.Equal(ErrorCodes.StaffLocationVerificationFailed, result.ErrorCode);
    }

    [Fact]
    public void Evaluate_rejectsMissingBusinessCoordinates()
    {
        var result = StaffAttendanceGeofenceEvaluator.EvaluateForClockIn(
            true,
            true,
            null,
            3.3792,
            100,
            new StaffAttendanceGeofenceEvaluator.LocationFix(6.5244, 3.3792, 10, Now),
            Now);
        Assert.Equal(ErrorCodes.AttendanceLocationNotConfigured, result.ErrorCode);
    }
}
