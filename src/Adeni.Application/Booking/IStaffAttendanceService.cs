namespace Adeni.Application.Booking;

using Adeni.Application.Auth;
using Adeni.Domain.Common;

public sealed record StaffAttendanceEventResponse(
    Guid Id,
    string EventType,
    DateTimeOffset OccurredAt,
    Guid BusinessLocationId,
    string LocationVerificationStatus,
    string VerificationMethod);

public sealed record StaffAttendanceScheduleWindowResponse(
    string OpenTime,
    string CloseTime);

public sealed record StaffAttendanceTodayResponse(
    Guid StaffMemberId,
    Guid BusinessLocationId,
    string WorkState,
    IReadOnlyList<StaffAttendanceEventResponse> EventsToday,
    IReadOnlyList<StaffAttendanceScheduleWindowResponse> ScheduledWindows,
    bool AttendanceGeofenceEnabled,
    bool AttendanceGeofenceRequired,
    bool LocationHasCoordinates,
    int AttendanceGeofenceRadiusMeters,
    string? LocationName);

public sealed record StaffAttendanceLocationFixRequest(
    double Latitude,
    double Longitude,
    int AccuracyMeters,
    DateTimeOffset CapturedAt);

public sealed record RecordStaffAttendanceEventRequest(
    string EventType,
    Guid BusinessLocationId,
    StaffAttendanceLocationFixRequest? LocationFix = null);

public sealed record ManagerStaffAttendanceOverrideRequest(
    string EventType,
    Guid BusinessLocationId,
    string Reason);

public sealed record StaffAttendanceTeamMemberResponse(
    Guid StaffMemberId,
    string DisplayName,
    string WorkState,
    DateTimeOffset? LastEventAt,
    string? LastEventType);

public sealed record StaffAttendanceTeamResponse(
    DateOnly Date,
    Guid? BusinessLocationId,
    IReadOnlyList<StaffAttendanceTeamMemberResponse> Members);

public sealed record StaffAttendanceScheduleTodayResponse(
    Guid StaffMemberId,
    DateOnly Date,
    IReadOnlyList<StaffAttendanceScheduleWindowResponse> ScheduledWindows,
    string? LocationName);

public sealed record StaffAttendanceHistoryResponse(
    Guid StaffMemberId,
    string DisplayName,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<StaffAttendanceEventResponse> Events);

public sealed record AppendStaffAttendanceCorrectionRequest(
    Guid RelatedEventId,
    string Reason);

public interface IStaffAttendanceService
{
    Task<Result<StaffAttendanceTodayResponse>> GetMyTodayAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        CancellationToken cancellationToken = default);

    Task<Result<StaffAttendanceScheduleTodayResponse>> GetMyScheduleTodayAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        CancellationToken cancellationToken = default);

    Task<Result<StaffAttendanceHistoryResponse>> GetStaffHistoryAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        Guid staffMemberId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<Result<StaffAttendanceEventResponse>> AppendStaffCorrectionAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        Guid staffMemberId,
        AppendStaffAttendanceCorrectionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<StaffAttendanceEventResponse>> RecordMyEventAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        RecordStaffAttendanceEventRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<StaffAttendanceTeamResponse>> GetTeamAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        DateOnly date,
        Guid? businessLocationId,
        CancellationToken cancellationToken = default);

    Task<Result<StaffAttendanceEventResponse>> RecordManagerOverrideAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        Guid staffMemberId,
        ManagerStaffAttendanceOverrideRequest request,
        CancellationToken cancellationToken = default);
}
