namespace Adeni.Application.Booking;

public sealed record QueueExpectedArrivalResponse(
    Guid BookingId,
    DateTimeOffset StartAt,
    string DisplayName,
    string? Phone,
    string? ServiceName,
    string? StaffDisplayName,
    Guid? StaffMemberId);

public sealed record WalkInStatusResponse(
    bool AcceptingNow,
    bool WalkInsEnabled,
    DateTimeOffset? AcceptingUntil,
    int? MaxWaiting,
    string CalendarPolicy);

public sealed record TenantQueueFloorResponse(
    IReadOnlyList<QueueTicketResponse> Items,
    IReadOnlyList<QueueExpectedArrivalResponse> ExpectedArrivals,
    WalkInStatusResponse WalkInStatus);

public sealed record UpdateWalkInSettingsRequest(
    bool WalkInsEnabled,
    int? MaxWalkInWaiting,
    string CalendarPolicy);

public sealed record OpenWalkInBlockRequest(int DurationMinutes);

public sealed record WalkInSettingsResponse(
    bool WalkInsEnabled,
    DateTimeOffset? WalkInAcceptingUntil,
    int? MaxWalkInWaiting,
    string CalendarPolicy,
    bool AcceptingNow);
