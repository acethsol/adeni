namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Abstractions;
using Adeni.Application.Auth;
using Adeni.Application.Booking;
using Adeni.Application.Identity;
using Adeni.Application.Subscriptions;
using Adeni.Application.Tenancy;
using Adeni.Domain.Auditing;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class VerifiedPresenceService(
    AdeniDbContext dbContext,
    IEntitlementsService entitlementsService,
    ILocationSecurityPolicyService locationSecurityPolicyService,
    IAuditLogWriter auditLogWriter,
    ICurrentUser currentUser,
    ICorrelationContext correlationContext,
    TimeProvider clock) : IVerifiedPresenceService
{
    public const int DefaultValidityMinutes = 90;
    public const int MinExtensionIntervalMinutes = 30;

    public async Task<Result<VerifiedPresenceSessionResponse?>> GetMyActiveSessionAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureEntitledAsync(tenantId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<VerifiedPresenceSessionResponse?>(gate.Error);
        }

        var session = await LoadActiveSessionAsync(tenantId, actor.BusinessUserId, cancellationToken);
        if (session is null)
        {
            return Result.Success<VerifiedPresenceSessionResponse?>(null);
        }

        var now = clock.GetUtcNow();
        if (session.ExpiresAt <= now)
        {
            await InvalidateSessionAsync(session, VerifiedPresenceInvalidationReasons.Expired, now, cancellationToken);
            return Result.Success<VerifiedPresenceSessionResponse?>(null);
        }

        return Result.Success<VerifiedPresenceSessionResponse?>(ToResponse(session));
    }

    public async Task<Result<VerifiedPresenceSessionResponse>> VerifyAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        VerifyPresenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureEntitledAsync(tenantId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<VerifiedPresenceSessionResponse>(gate.Error);
        }

        if (!actor.Permissions.Contains(BusinessPermissions.AttendanceSelf))
        {
            return Result.Failure<VerifiedPresenceSessionResponse>(ErrorCodes.PermissionDeniedError());
        }

        if (actor.StaffMemberId is not { } staffMemberId)
        {
            return Result.Failure<VerifiedPresenceSessionResponse>(ErrorCodes.AttendanceStaffNotLinkedError());
        }

        if (!await IsOnShiftAsync(tenantId, staffMemberId, cancellationToken))
        {
            return Result.Failure<VerifiedPresenceSessionResponse>(
                Error.Validation("Verify presence while you are on shift (clocked in)."));
        }

        var location = await dbContext.BusinessLocations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.BusinessLocationId && x.IsActive, cancellationToken);
        if (location is null)
        {
            return Result.Failure<VerifiedPresenceSessionResponse>(Error.NotFound("Business location"));
        }

        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<VerifiedPresenceSessionResponse>(Error.NotFound("Tenant"));
        }

        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
        var now = clock.GetUtcNow();
        var geofenceRequired = location.AttendanceGeofenceEnabled
            && entitlements.AttendanceGeofence
            && location.Latitude is not null
            && location.Longitude is not null;

        string verificationMethod;
        int? distance = null;
        int? accuracy = null;

        if (geofenceRequired)
        {
            var evaluation = StaffAttendanceGeofenceEvaluator.EvaluateForClockIn(
                true,
                true,
                location.Latitude,
                location.Longitude,
                location.AttendanceGeofenceRadiusMeters,
                MapFix(request.LocationFix),
                now);
            if (evaluation.ErrorCode is not null)
            {
                return Result.Failure<VerifiedPresenceSessionResponse>(evaluation.ErrorCode switch
                {
                    ErrorCodes.AttendanceLocationNotConfigured => ErrorCodes.AttendanceLocationNotConfiguredError(),
                    _ => ErrorCodes.StaffLocationVerificationFailedError(
                        evaluation.DistanceFromLocationMeters,
                        StaffAttendanceGeofenceEvaluator.ClampRadius(location.AttendanceGeofenceRadiusMeters)),
                });
            }

            verificationMethod = VerifiedPresenceVerificationMethods.Geofence;
            distance = evaluation.DistanceFromLocationMeters;
            accuracy = evaluation.ReportedAccuracyMeters;
        }
        else
        {
            verificationMethod = VerifiedPresenceVerificationMethods.ManualVerify;
        }

        var active = await LoadActiveSessionTrackedAsync(tenantId, actor.BusinessUserId, cancellationToken);
        if (active is not null)
        {
            if (active.BusinessLocationId != request.BusinessLocationId)
            {
                await InvalidateSessionAsync(active, VerifiedPresenceInvalidationReasons.LocationSwitch, now, cancellationToken);
            }
            else if (active.ExpiresAt > now && !CanExtend(active, now))
            {
                return Result.Success(ToResponse(active));
            }
            else if (active.ExpiresAt > now && CanExtend(active, now))
            {
                active.ExpiresAt = now.AddMinutes(await ResolveValidityMinutesAsync(tenantId, cancellationToken));
                active.LastExtendedAt = now;
                active.VerificationMethod = verificationMethod;
                active.DistanceFromLocationMeters = distance;
                active.ReportedAccuracyMeters = accuracy;
                await dbContext.SaveChangesAsync(cancellationToken);
                await WriteVerifiedAuditAsync(tenantId, active, cancellationToken);
                return Result.Success(ToResponse(active));
            }
        }

        if (active is not null && active.InvalidatedAt is null && active.ExpiresAt <= now)
        {
            await InvalidateSessionAsync(active, VerifiedPresenceInvalidationReasons.Expired, now, cancellationToken);
        }

        await InvalidateAllActiveAsync(tenantId, actor.BusinessUserId, VerifiedPresenceInvalidationReasons.Replaced, now, cancellationToken);

        var session = new VerifiedPresenceSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessUserId = actor.BusinessUserId,
            StaffMemberId = staffMemberId,
            BusinessLocationId = request.BusinessLocationId,
            VerifiedAt = now,
            ExpiresAt = now.AddMinutes(await ResolveValidityMinutesAsync(tenantId, cancellationToken)),
            VerificationMethod = verificationMethod,
            DistanceFromLocationMeters = distance,
            ReportedAccuracyMeters = accuracy,
        };
        dbContext.VerifiedPresenceSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteVerifiedAuditAsync(tenantId, session, cancellationToken);
        return Result.Success(ToResponse(session));
    }

    public async Task<Result> InvalidateMySessionAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureEntitledAsync(tenantId, cancellationToken);
        if (gate.IsFailure)
        {
            return gate;
        }

        var now = clock.GetUtcNow();
        await InvalidateAllActiveAsync(
            tenantId,
            actor.BusinessUserId,
            VerifiedPresenceInvalidationReasons.Logout,
            now,
            cancellationToken);
        return Result.Success();
    }

    public async Task ApplyAttendanceEventAsync(
        Guid tenantId,
        StaffAttendanceEvent attendanceEvent,
        Guid presenceBusinessUserId,
        Guid? staffMemberId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        if (attendanceEvent.EventType == StaffAttendanceEventTypes.ClockOut)
        {
            await InvalidateAllActiveAsync(
                tenantId,
                presenceBusinessUserId,
                VerifiedPresenceInvalidationReasons.ClockOut,
                now,
                cancellationToken);
            return;
        }

        if (attendanceEvent.EventType != StaffAttendanceEventTypes.ClockIn)
        {
            return;
        }

        var verificationMethod = attendanceEvent.VerificationMethod switch
        {
            StaffAttendanceVerificationMethods.Override => VerifiedPresenceVerificationMethods.ManagerOverride,
            StaffAttendanceVerificationMethods.Geofence => VerifiedPresenceVerificationMethods.AttendanceClockIn,
            _ => VerifiedPresenceVerificationMethods.AttendanceClockIn,
        };

        await InvalidateAllActiveAsync(
            tenantId,
            presenceBusinessUserId,
            VerifiedPresenceInvalidationReasons.Replaced,
            now,
            cancellationToken);

        var session = new VerifiedPresenceSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessUserId = presenceBusinessUserId,
            StaffMemberId = staffMemberId,
            BusinessLocationId = attendanceEvent.BusinessLocationId,
            VerifiedAt = attendanceEvent.OccurredAt,
            ExpiresAt = attendanceEvent.OccurredAt.AddMinutes(
                (await locationSecurityPolicyService.ResolvePolicyAsync(tenantId, cancellationToken)).ValidityMinutes),
            VerificationMethod = verificationMethod,
            DistanceFromLocationMeters = attendanceEvent.DistanceFromLocationMeters,
            ReportedAccuracyMeters = attendanceEvent.ReportedAccuracyMeters,
            SourceAttendanceEventId = attendanceEvent.Id,
        };
        dbContext.VerifiedPresenceSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteVerifiedAuditAsync(tenantId, session, cancellationToken);
    }

    private static bool CanExtend(VerifiedPresenceSession session, DateTimeOffset now)
    {
        var anchor = session.LastExtendedAt ?? session.VerifiedAt;
        return (now - anchor).TotalMinutes >= MinExtensionIntervalMinutes;
    }

    private async Task<bool> IsOnShiftAsync(Guid tenantId, Guid staffMemberId, CancellationToken cancellationToken)
    {
        var openTypes = await LoadOpenShiftEventTypesAsync(tenantId, staffMemberId, cancellationToken);
        var state = StaffAttendanceStateMachine.DeriveState(openTypes);
        return state is StaffAttendanceWorkStates.Working or StaffAttendanceWorkStates.OnBreak;
    }

    private async Task<IReadOnlyList<string>> LoadOpenShiftEventTypesAsync(
        Guid tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        var events = await dbContext.StaffAttendanceEvents.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.StaffMemberId == staffMemberId)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.RecordedAt)
            .Select(x => new { x.EventType, x.OccurredAt })
            .ToListAsync(cancellationToken);

        var openShift = new List<string>();
        foreach (var row in events)
        {
            if (row.EventType == StaffAttendanceEventTypes.ClockOut)
            {
                openShift.Clear();
                continue;
            }

            if (row.EventType is StaffAttendanceEventTypes.ClockIn
                or StaffAttendanceEventTypes.BreakStart
                or StaffAttendanceEventTypes.BreakEnd)
            {
                openShift.Add(row.EventType);
            }
        }

        return openShift;
    }

    private async Task<VerifiedPresenceSession?> LoadActiveSessionAsync(
        Guid tenantId,
        Guid businessUserId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await dbContext.VerifiedPresenceSessions.AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.BusinessUserId == businessUserId
                && x.InvalidatedAt == null
                && x.ExpiresAt > now)
            .OrderByDescending(x => x.VerifiedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<VerifiedPresenceSession?> LoadActiveSessionTrackedAsync(
        Guid tenantId,
        Guid businessUserId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await dbContext.VerifiedPresenceSessions
            .Where(x => x.TenantId == tenantId
                && x.BusinessUserId == businessUserId
                && x.InvalidatedAt == null
                && x.ExpiresAt > now)
            .OrderByDescending(x => x.VerifiedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task InvalidateAllActiveAsync(
        Guid tenantId,
        Guid businessUserId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.VerifiedPresenceSessions
            .Where(x => x.TenantId == tenantId
                && x.BusinessUserId == businessUserId
                && x.InvalidatedAt == null
                && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            await InvalidateSessionAsync(session, reason, now, cancellationToken);
        }
    }

    private async Task InvalidateSessionAsync(
        VerifiedPresenceSession session,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (session.InvalidatedAt is not null)
        {
            return;
        }

        session.InvalidatedAt = now;
        session.InvalidationReason = reason;
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteInvalidatedAuditAsync(session.TenantId, session, cancellationToken);
    }

    private async Task<Result> EnsureEntitledAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure(Error.NotFound("Tenant"));
        }

        if (!entitlementsService.GetEntitlementsForTenant(tenant).StaffAttendance)
        {
            return Result.Failure(ErrorCodes.AttendanceNotEntitledError());
        }

        return Result.Success();
    }

    private async Task<int> ResolveValidityMinutesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var (_, minutes) = await locationSecurityPolicyService.ResolvePolicyAsync(tenantId, cancellationToken);
        return minutes;
    }

    private static VerifiedPresenceSessionResponse ToResponse(VerifiedPresenceSession session) =>
        new(
            session.Id,
            session.BusinessLocationId,
            session.VerificationMethod,
            session.VerifiedAt,
            session.ExpiresAt,
            session.DistanceFromLocationMeters,
            session.ReportedAccuracyMeters);

    private static StaffAttendanceGeofenceEvaluator.LocationFix? MapFix(StaffAttendanceLocationFixRequest? fix) =>
        fix is null
            ? null
            : new StaffAttendanceGeofenceEvaluator.LocationFix(
                fix.Latitude,
                fix.Longitude,
                fix.AccuracyMeters,
                fix.CapturedAt);

    private async Task WriteVerifiedAuditAsync(
        Guid tenantId,
        VerifiedPresenceSession session,
        CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? "portal:unknown";
        await auditLogWriter.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actorId,
                AuditActions.StaffPresenceVerified,
                "verified_presence",
                session.Id.ToString(),
                correlationContext.CorrelationId,
                clock.GetUtcNow(),
                $"{{\"tenantId\":\"{tenantId}\",\"businessLocationId\":\"{session.BusinessLocationId}\",\"method\":\"{session.VerificationMethod}\"}}"),
            cancellationToken);
    }

    private async Task WriteInvalidatedAuditAsync(
        Guid tenantId,
        VerifiedPresenceSession session,
        CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? "portal:unknown";
        await auditLogWriter.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actorId,
                AuditActions.StaffPresenceInvalidated,
                "verified_presence",
                session.Id.ToString(),
                correlationContext.CorrelationId,
                clock.GetUtcNow(),
                $"{{\"tenantId\":\"{tenantId}\",\"reason\":\"{session.InvalidationReason}\",\"businessLocationId\":\"{session.BusinessLocationId}\"}}"),
            cancellationToken);
    }
}
