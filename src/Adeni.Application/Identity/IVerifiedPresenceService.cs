namespace Adeni.Application.Identity;

using Adeni.Application.Auth;
using Adeni.Application.Booking;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;

public sealed record VerifiedPresenceSessionResponse(
    Guid Id,
    Guid BusinessLocationId,
    string VerificationMethod,
    DateTimeOffset VerifiedAt,
    DateTimeOffset ExpiresAt,
    int? DistanceFromLocationMeters,
    int? ReportedAccuracyMeters);

public sealed record VerifyPresenceRequest(
    Guid BusinessLocationId,
    StaffAttendanceLocationFixRequest? LocationFix = null);

public interface IVerifiedPresenceService
{
    Task<Result<VerifiedPresenceSessionResponse?>> GetMyActiveSessionAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        CancellationToken cancellationToken = default);

    Task<Result<VerifiedPresenceSessionResponse>> VerifyAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        VerifyPresenceRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> InvalidateMySessionAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        CancellationToken cancellationToken = default);

    Task ApplyAttendanceEventAsync(
        Guid tenantId,
        StaffAttendanceEvent attendanceEvent,
        Guid presenceBusinessUserId,
        Guid? staffMemberId,
        CancellationToken cancellationToken = default);
}
