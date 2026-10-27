namespace Adeni.Application.Calendar;

using Adeni.Domain.Common;

public sealed record CalendarConnectionView(Guid Id, Guid StaffMemberId, string Provider, string Status,
    DateTimeOffset? LastSyncedAt, DateTimeOffset? CoveredUntil, string? FailureCategory,
    IReadOnlyList<CalendarSelectionView> Selections);
public sealed record CalendarSelectionView(string Id, string TimeZoneId, bool IsWriteTarget);
public sealed record ExternalCalendarView(string Id, string Name, string TimeZoneId, bool CanWrite);
public sealed record CalendarConnectRequest(Guid StaffMemberId, string Provider, bool WriteAccess = false);
public sealed record CalendarAuthorizationView(string LaunchUrl) { public override string ToString() => "[Calendar authorization]"; }
public sealed record CalendarSelectionRequest(IReadOnlyList<CalendarSelectionView> Calendars);
public sealed record CalendarConflictView(Guid Id, Guid StaffMemberId, Guid BookingId, DateTimeOffset DetectedAt,
    DateTimeOffset? AcknowledgedAt, DateTimeOffset? ResolvedAt, string Category, DateTimeOffset StartAt, DateTimeOffset EndAt);
public sealed record CalendarOAuthLaunch(string AuthorizationUrl, string Correlation) { public override string ToString() => "[Calendar OAuth launch]"; }
public sealed record CalendarBusyInterval(DateTimeOffset StartAt, DateTimeOffset EndAt);
public sealed record CalendarStaffAvailability(Guid StaffMemberId, bool HasCoverage, IReadOnlyList<CalendarBusyInterval> Busy)
{
    public bool IsFree(DateTimeOffset start, DateTimeOffset end) => HasCoverage && !Busy.Any(x => x.StartAt < end && x.EndAt > start);
}
public interface IExternalCalendarAvailability
{
    bool IsAdmissionConflict(Exception error);
    Task<IReadOnlyList<CalendarStaffAvailability>> ReadAsync(Guid tenantId, IReadOnlyList<Guid> staffIds,
        DateTimeOffset start, DateTimeOffset end, bool stageAdmission, CancellationToken ct);
}
public interface ICalendarAccessQuery
{
    Task<bool> CanManageAsync(Guid tenantId, string actor, Guid? staffId, CancellationToken ct);
}
public sealed record CalendarBookingFact(Guid Id, Guid TenantId, Guid? StaffMemberId, DateTimeOffset StartAt,
    DateTimeOffset EndAt, bool BlocksAvailability, bool ShouldWrite, Guid? ServiceId = null, Guid? LocationId = null, bool IsInProgress = false, bool IsFinal = false);
public interface ICalendarBookingQuery
{
    Task<IReadOnlyList<CalendarBookingFact>> ReadAsync(Guid tenant, DateTimeOffset start, DateTimeOffset end,
        IReadOnlyList<Guid> mappedIds, CancellationToken ct);
}
public interface ICalendarService
{
    Task<Result<IReadOnlyList<CalendarConnectionView>>> ListAsync(Guid tenant, string actor, CancellationToken ct);
    Task<Result<CalendarAuthorizationView>> ConnectAsync(Guid tenant, string actor, CalendarConnectRequest request, CancellationToken ct);
    Task<Result<CalendarOAuthLaunch>> LaunchAsync(string ticket, CancellationToken ct);
    Task<Result<bool>> CallbackAsync(string provider, string state, string correlation, string code, CancellationToken ct);
    Task<Result<IReadOnlyList<ExternalCalendarView>>> CalendarsAsync(Guid tenant, string actor, Guid id, CancellationToken ct);
    Task<Result<bool>> SelectAsync(Guid tenant, string actor, Guid id, CalendarSelectionRequest request, CancellationToken ct);
    Task<Result<bool>> DisconnectAsync(Guid tenant, string actor, Guid id, CancellationToken ct);
    Task<Result<bool>> SyncAsync(Guid tenant, string actor, Guid id, CancellationToken ct);
    Task<Result<IReadOnlyList<CalendarConflictView>>> ConflictsAsync(Guid tenant, string actor, CancellationToken ct);
    Task<Result<bool>> AcknowledgeAsync(Guid tenant, string actor, Guid id, CancellationToken ct);
}
