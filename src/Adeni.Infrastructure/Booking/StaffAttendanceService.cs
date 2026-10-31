namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Abstractions;
using Adeni.Application.Auth;
using Adeni.Application.Booking;
using Adeni.Application.Identity;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Auditing;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class StaffAttendanceService(
    AdeniDbContext dbContext,
    IEntitlementsService entitlementsService,
    IVerifiedPresenceService verifiedPresenceService,
    IAuditLogWriter auditLogWriter,
    ICurrentUser currentUser,
    ICorrelationContext correlationContext,
    TimeProvider clock) : IStaffAttendanceService
{
    public async Task<Result<StaffAttendanceTodayResponse>> GetMyTodayAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureEntitledAsync(tenantId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<StaffAttendanceTodayResponse>(gate.Error);
        }

        if (actor.StaffMemberId is not { } staffMemberId)
        {
            return Result.Failure<StaffAttendanceTodayResponse>(ErrorCodes.AttendanceStaffNotLinkedError());
        }

        if (!actor.Permissions.Contains(BusinessPermissions.AttendanceSelf))
        {
            return Result.Failure<StaffAttendanceTodayResponse>(ErrorCodes.PermissionDeniedError());
        }

        var staff = await dbContext.StaffMembers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == staffMemberId, cancellationToken);
        if (staff is null)
        {
            return Result.Failure<StaffAttendanceTodayResponse>(Error.NotFound("Staff member"));
        }

        var locationId = staff.BusinessLocationId
            ?? await ResolvePrimaryLocationIdAsync(tenantId, cancellationToken);
        if (locationId is null)
        {
            return Result.Failure<StaffAttendanceTodayResponse>(Error.Validation("No active business location."));
        }

        var location = await dbContext.BusinessLocations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == locationId, cancellationToken);
        if (location is null)
        {
            return Result.Failure<StaffAttendanceTodayResponse>(Error.NotFound("Business location"));
        }

        var (dayStart, dayEnd) = GetLocationDayBounds(date: DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(clock.GetUtcNow(), ResolveTimeZone(location.TimeZoneId)).DateTime), location.TimeZoneId);

        var events = await LoadShiftEventsAsync(tenantId, staffMemberId, dayStart, dayEnd, cancellationToken);
        var openShiftEvents = await LoadOpenShiftEventTypesAsync(tenantId, staffMemberId, cancellationToken);
        var state = StaffAttendanceStateMachine.DeriveState(openShiftEvents);

        var scheduled = await LoadScheduleWindowsAsync(tenantId, staffMemberId, dayStart, location.TimeZoneId, cancellationToken);

        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        var entitlements = tenant is null
            ? null
            : entitlementsService.GetEntitlementsForTenant(tenant);
        var hasCoords = location.Latitude is not null && location.Longitude is not null;
        var geofenceRequired = location.AttendanceGeofenceEnabled
            && entitlements?.AttendanceGeofence == true
            && hasCoords;

        return Result.Success(new StaffAttendanceTodayResponse(
            staffMemberId,
            locationId.Value,
            state,
            events.Select(ToResponse).ToList(),
            scheduled,
            location.AttendanceGeofenceEnabled,
            geofenceRequired,
            hasCoords,
            location.AttendanceGeofenceRadiusMeters,
            location.Name));
    }

    public async Task<Result<StaffAttendanceScheduleTodayResponse>> GetMyScheduleTodayAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureEntitledAsync(tenantId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<StaffAttendanceScheduleTodayResponse>(gate.Error);
        }

        if (actor.StaffMemberId is not { } staffMemberId)
        {
            return Result.Failure<StaffAttendanceScheduleTodayResponse>(ErrorCodes.AttendanceStaffNotLinkedError());
        }

        if (!actor.Permissions.Contains(BusinessPermissions.AttendanceSelf))
        {
            return Result.Failure<StaffAttendanceScheduleTodayResponse>(ErrorCodes.PermissionDeniedError());
        }

        var staff = await dbContext.StaffMembers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == staffMemberId, cancellationToken);
        if (staff is null)
        {
            return Result.Failure<StaffAttendanceScheduleTodayResponse>(Error.NotFound("Staff member"));
        }

        var locationId = staff.BusinessLocationId
            ?? await ResolvePrimaryLocationIdAsync(tenantId, cancellationToken);
        if (locationId is null)
        {
            return Result.Failure<StaffAttendanceScheduleTodayResponse>(Error.Validation("No active business location."));
        }

        var location = await dbContext.BusinessLocations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == locationId, cancellationToken);
        if (location is null)
        {
            return Result.Failure<StaffAttendanceScheduleTodayResponse>(Error.NotFound("Business location"));
        }

        var date = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(clock.GetUtcNow(), ResolveTimeZone(location.TimeZoneId)).DateTime);
        var (dayStart, _) = GetLocationDayBounds(date, location.TimeZoneId);
        var scheduled = await LoadScheduleWindowsAsync(tenantId, staffMemberId, dayStart, location.TimeZoneId, cancellationToken);

        return Result.Success(new StaffAttendanceScheduleTodayResponse(
            staffMemberId,
            date,
            scheduled,
            location.Name));
    }

    public async Task<Result<StaffAttendanceHistoryResponse>> GetStaffHistoryAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        Guid staffMemberId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureEntitledAsync(tenantId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<StaffAttendanceHistoryResponse>(gate.Error);
        }

        var isSelf = actor.StaffMemberId == staffMemberId;
        if (isSelf)
        {
            if (!actor.Permissions.Contains(BusinessPermissions.AttendanceSelf))
            {
                return Result.Failure<StaffAttendanceHistoryResponse>(ErrorCodes.PermissionDeniedError());
            }
        }
        else if (!actor.Permissions.Contains(BusinessPermissions.Attendance))
        {
            return Result.Failure<StaffAttendanceHistoryResponse>(ErrorCodes.PermissionDeniedError());
        }

        if (to < from)
        {
            return Result.Failure<StaffAttendanceHistoryResponse>(Error.Validation("'to' must be on or after 'from'."));
        }

        if ((to.ToDateTime(TimeOnly.MinValue) - from.ToDateTime(TimeOnly.MinValue)).TotalDays > 90)
        {
            return Result.Failure<StaffAttendanceHistoryResponse>(Error.Validation("Date range cannot exceed 90 days."));
        }

        var staff = await dbContext.StaffMembers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == staffMemberId, cancellationToken);
        if (staff is null)
        {
            return Result.Failure<StaffAttendanceHistoryResponse>(Error.NotFound("Staff member"));
        }

        var timeZoneId = staff.BusinessLocationId is Guid locId
            ? await dbContext.BusinessLocations.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Id == locId)
                .Select(x => x.TimeZoneId)
                .FirstOrDefaultAsync(cancellationToken) ?? "UTC"
            : await dbContext.BusinessLocations.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.IsPrimary && x.IsActive)
                .Select(x => x.TimeZoneId)
                .FirstOrDefaultAsync(cancellationToken) ?? "UTC";

        var (rangeStart, _) = GetLocationDayBounds(from, timeZoneId);
        var (_, rangeEnd) = GetLocationDayBounds(to, timeZoneId);

        var events = await dbContext.StaffAttendanceEvents.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.StaffMemberId == staffMemberId
                && x.OccurredAt >= rangeStart && x.OccurredAt < rangeEnd)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.RecordedAt)
            .ToListAsync(cancellationToken);

        return Result.Success(new StaffAttendanceHistoryResponse(
            staffMemberId,
            staff.DisplayName,
            from,
            to,
            events.Select(ToResponse).ToList()));
    }

    public async Task<Result<StaffAttendanceEventResponse>> AppendStaffCorrectionAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        Guid staffMemberId,
        AppendStaffAttendanceCorrectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureEntitledAsync(tenantId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<StaffAttendanceEventResponse>(gate.Error);
        }

        if (!actor.Permissions.Contains(BusinessPermissions.Attendance))
        {
            return Result.Failure<StaffAttendanceEventResponse>(ErrorCodes.PermissionDeniedError());
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
        {
            return Result.Failure<StaffAttendanceEventResponse>(Error.Validation("Correction reason must be at least 10 characters."));
        }

        var related = await dbContext.StaffAttendanceEvents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.RelatedEventId, cancellationToken);
        if (related is null || related.StaffMemberId != staffMemberId)
        {
            return Result.Failure<StaffAttendanceEventResponse>(Error.NotFound("Related attendance event"));
        }

        var staff = await dbContext.StaffMembers
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == staffMemberId, cancellationToken);
        if (staff is null || !staff.IsActive)
        {
            return Result.Failure<StaffAttendanceEventResponse>(Error.NotFound("Staff member"));
        }

        var now = clock.GetUtcNow();
        var entity = new StaffAttendanceEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessLocationId = related.BusinessLocationId,
            StaffMemberId = staffMemberId,
            BusinessUserId = actor.BusinessUserId,
            EventType = StaffAttendanceEventTypes.Correction,
            OccurredAt = now,
            RecordedAt = now,
            LocationVerificationStatus = StaffLocationVerificationStatuses.NotRequired,
            VerificationMethod = StaffAttendanceVerificationMethods.None,
            RelatedEventId = request.RelatedEventId,
            CorrectionReason = request.Reason.Trim(),
        };

        dbContext.StaffAttendanceEvents.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteCorrectionAuditAsync(tenantId, entity, cancellationToken);

        return Result.Success(ToResponse(entity));
    }

    public async Task<Result<StaffAttendanceEventResponse>> RecordMyEventAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        RecordStaffAttendanceEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureEntitledAsync(tenantId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<StaffAttendanceEventResponse>(gate.Error);
        }

        if (actor.StaffMemberId is not { } staffMemberId)
        {
            return Result.Failure<StaffAttendanceEventResponse>(ErrorCodes.AttendanceStaffNotLinkedError());
        }

        if (!actor.Permissions.Contains(BusinessPermissions.AttendanceSelf))
        {
            return Result.Failure<StaffAttendanceEventResponse>(ErrorCodes.PermissionDeniedError());
        }

        if (!StaffAttendanceEventTypes.All.Contains(request.EventType)
            || request.EventType is StaffAttendanceEventTypes.Correction or StaffAttendanceEventTypes.Void)
        {
            return Result.Failure<StaffAttendanceEventResponse>(Error.Validation("Invalid attendance event type."));
        }

        var staff = await dbContext.StaffMembers
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == staffMemberId, cancellationToken);
        if (staff is null || !staff.IsActive)
        {
            return Result.Failure<StaffAttendanceEventResponse>(Error.NotFound("Staff member"));
        }

        var location = await dbContext.BusinessLocations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.BusinessLocationId && x.IsActive, cancellationToken);
        if (location is null)
        {
            return Result.Failure<StaffAttendanceEventResponse>(Error.NotFound("Business location"));
        }

        var openShiftTypes = await LoadOpenShiftEventTypesAsync(tenantId, staffMemberId, cancellationToken);
        var currentState = StaffAttendanceStateMachine.DeriveState(openShiftTypes);
        if (!StaffAttendanceStateMachine.CanApply(currentState, request.EventType))
        {
            return Result.Failure<StaffAttendanceEventResponse>(ErrorCodes.AttendanceInvalidTransitionError());
        }

        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<StaffAttendanceEventResponse>(Error.NotFound("Tenant"));
        }

        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
        var now = clock.GetUtcNow();
        var verification = request.EventType == StaffAttendanceEventTypes.ClockIn
            ? StaffAttendanceGeofenceEvaluator.EvaluateForClockIn(
                location.AttendanceGeofenceEnabled,
                entitlements.AttendanceGeofence,
                location.Latitude,
                location.Longitude,
                location.AttendanceGeofenceRadiusMeters,
                MapFix(request.LocationFix),
                now)
            : new StaffAttendanceGeofenceEvaluator.Evaluation(
                StaffLocationVerificationStatuses.NotRequired,
                StaffAttendanceVerificationMethods.None,
                null,
                null,
                null);

        if (verification.ErrorCode is not null)
        {
            return Result.Failure<StaffAttendanceEventResponse>(verification.ErrorCode switch
            {
                ErrorCodes.AttendanceLocationNotConfigured => ErrorCodes.AttendanceLocationNotConfiguredError(),
                _ => ErrorCodes.StaffLocationVerificationFailedError(
                    verification.DistanceFromLocationMeters,
                    StaffAttendanceGeofenceEvaluator.ClampRadius(location.AttendanceGeofenceRadiusMeters)),
            });
        }

        var entity = new StaffAttendanceEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessLocationId = request.BusinessLocationId,
            StaffMemberId = staffMemberId,
            BusinessUserId = actor.BusinessUserId,
            EventType = request.EventType,
            OccurredAt = now,
            RecordedAt = now,
            LocationVerificationStatus = verification.LocationVerificationStatus,
            VerificationMethod = verification.VerificationMethod,
            DistanceFromLocationMeters = verification.DistanceFromLocationMeters,
            ReportedAccuracyMeters = verification.ReportedAccuracyMeters,
            ClientLocationCapturedAt = request.LocationFix?.CapturedAt,
        };

        dbContext.StaffAttendanceEvents.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(tenantId, entity, cancellationToken);
        await verifiedPresenceService.ApplyAttendanceEventAsync(
            tenantId,
            entity,
            actor.BusinessUserId,
            actor.StaffMemberId,
            cancellationToken);

        return Result.Success(ToResponse(entity));
    }

    public async Task<Result<StaffAttendanceEventResponse>> RecordManagerOverrideAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        Guid staffMemberId,
        ManagerStaffAttendanceOverrideRequest request,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureEntitledAsync(tenantId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<StaffAttendanceEventResponse>(gate.Error);
        }

        if (!actor.Permissions.Contains(BusinessPermissions.Attendance))
        {
            return Result.Failure<StaffAttendanceEventResponse>(ErrorCodes.PermissionDeniedError());
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
        {
            return Result.Failure<StaffAttendanceEventResponse>(Error.Validation("Override reason must be at least 10 characters."));
        }

        if (!StaffAttendanceEventTypes.All.Contains(request.EventType)
            || request.EventType is StaffAttendanceEventTypes.Correction or StaffAttendanceEventTypes.Void)
        {
            return Result.Failure<StaffAttendanceEventResponse>(Error.Validation("Invalid attendance event type."));
        }

        var staff = await dbContext.StaffMembers
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == staffMemberId, cancellationToken);
        if (staff is null || !staff.IsActive)
        {
            return Result.Failure<StaffAttendanceEventResponse>(Error.NotFound("Staff member"));
        }

        var location = await dbContext.BusinessLocations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.BusinessLocationId && x.IsActive, cancellationToken);
        if (location is null)
        {
            return Result.Failure<StaffAttendanceEventResponse>(Error.NotFound("Business location"));
        }

        var openShiftTypes = await LoadOpenShiftEventTypesAsync(tenantId, staffMemberId, cancellationToken);
        var currentState = StaffAttendanceStateMachine.DeriveState(openShiftTypes);
        if (!StaffAttendanceStateMachine.CanApply(currentState, request.EventType))
        {
            return Result.Failure<StaffAttendanceEventResponse>(ErrorCodes.AttendanceInvalidTransitionError());
        }

        var now = clock.GetUtcNow();
        var entity = new StaffAttendanceEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessLocationId = request.BusinessLocationId,
            StaffMemberId = staffMemberId,
            BusinessUserId = actor.BusinessUserId,
            EventType = request.EventType,
            OccurredAt = now,
            RecordedAt = now,
            LocationVerificationStatus = StaffLocationVerificationStatuses.Override,
            VerificationMethod = StaffAttendanceVerificationMethods.Override,
            ManagerOverrideByBusinessUserId = actor.BusinessUserId,
            ManagerOverrideReason = request.Reason.Trim(),
        };

        dbContext.StaffAttendanceEvents.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(tenantId, entity, cancellationToken);
        await WriteOverrideAuditAsync(tenantId, entity, cancellationToken);
        if (await ResolveBusinessUserIdForStaffAsync(tenantId, staffMemberId, cancellationToken) is Guid presenceUserId)
        {
            await verifiedPresenceService.ApplyAttendanceEventAsync(
                tenantId,
                entity,
                presenceUserId,
                staffMemberId,
                cancellationToken);
        }

        return Result.Success(ToResponse(entity));
    }

    private async Task<Guid?> ResolveBusinessUserIdForStaffAsync(
        Guid tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken) =>
        await dbContext.BusinessUsers.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.StaffMemberId == staffMemberId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static StaffAttendanceGeofenceEvaluator.LocationFix? MapFix(StaffAttendanceLocationFixRequest? fix) =>
        fix is null
            ? null
            : new StaffAttendanceGeofenceEvaluator.LocationFix(
                fix.Latitude,
                fix.Longitude,
                fix.AccuracyMeters,
                fix.CapturedAt);

    public async Task<Result<StaffAttendanceTeamResponse>> GetTeamAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        DateOnly date,
        Guid? businessLocationId,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureEntitledAsync(tenantId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result.Failure<StaffAttendanceTeamResponse>(gate.Error);
        }

        if (!actor.Permissions.Contains(BusinessPermissions.Attendance))
        {
            return Result.Failure<StaffAttendanceTeamResponse>(ErrorCodes.PermissionDeniedError());
        }

        var staffQuery = dbContext.StaffMembers.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive);
        if (businessLocationId is Guid locId)
        {
            staffQuery = staffQuery.Where(x => x.BusinessLocationId == locId || x.BusinessLocationId == null);
        }

        var members = await staffQuery.OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        var timeZoneId = businessLocationId is Guid selectedLoc
            ? await dbContext.BusinessLocations.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Id == selectedLoc)
                .Select(x => x.TimeZoneId)
                .FirstOrDefaultAsync(cancellationToken) ?? "UTC"
            : await dbContext.BusinessLocations.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.IsPrimary && x.IsActive)
                .Select(x => x.TimeZoneId)
                .FirstOrDefaultAsync(cancellationToken) ?? "UTC";

        var (dayStart, dayEnd) = GetLocationDayBounds(date, timeZoneId);
        var responses = new List<StaffAttendanceTeamMemberResponse>();
        foreach (var member in members)
        {
            var openTypes = await LoadOpenShiftEventTypesAsync(tenantId, member.Id, cancellationToken);
            var state = StaffAttendanceStateMachine.DeriveState(openTypes);
            var last = await dbContext.StaffAttendanceEvents.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.StaffMemberId == member.Id
                    && x.OccurredAt >= dayStart && x.OccurredAt < dayEnd)
                .OrderByDescending(x => x.OccurredAt)
                .FirstOrDefaultAsync(cancellationToken);

            responses.Add(new StaffAttendanceTeamMemberResponse(
                member.Id,
                member.DisplayName,
                state,
                last?.OccurredAt,
                last?.EventType));
        }

        return Result.Success(new StaffAttendanceTeamResponse(date, businessLocationId, responses));
    }

    private async Task<Result> EnsureEntitledAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure(Error.NotFound("Tenant"));
        }

        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
        if (!entitlements.StaffAttendance)
        {
            return Result.Failure(ErrorCodes.AttendanceNotEntitledError());
        }

        return Result.Success();
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

    private async Task<IReadOnlyList<StaffAttendanceEvent>> LoadShiftEventsAsync(
        Guid tenantId,
        Guid staffMemberId,
        DateTimeOffset dayStart,
        DateTimeOffset dayEnd,
        CancellationToken cancellationToken) =>
        await dbContext.StaffAttendanceEvents.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.StaffMemberId == staffMemberId
                && x.OccurredAt >= dayStart && x.OccurredAt < dayEnd)
            .OrderBy(x => x.OccurredAt)
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<StaffAttendanceScheduleWindowResponse>> LoadScheduleWindowsAsync(
        Guid tenantId,
        Guid staffMemberId,
        DateTimeOffset dayStart,
        string? timeZoneId,
        CancellationToken cancellationToken)
    {
        var dayOfWeek = TimeZoneInfo.ConvertTime(dayStart, ResolveTimeZone(timeZoneId)).DayOfWeek;
        var staffHours = await dbContext.StaffWeeklyAvailabilities.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.StaffMemberId == staffMemberId && x.DayOfWeek == dayOfWeek)
            .OrderBy(x => x.OpenTime)
            .ToListAsync(cancellationToken);

        if (staffHours.Count == 0)
        {
            return [];
        }

        return staffHours
            .Select(x => new StaffAttendanceScheduleWindowResponse(
                x.OpenTime.ToString("HH:mm"),
                x.CloseTime.ToString("HH:mm")))
            .ToList();
    }

    private async Task<Guid?> ResolvePrimaryLocationIdAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.BusinessLocations.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsPrimary)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static (DateTimeOffset Start, DateTimeOffset End) GetLocationDayBounds(DateOnly date, string? timeZoneId)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var localMidnight = date.ToDateTime(TimeOnly.MinValue);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localMidnight, DateTimeKind.Unspecified), tz);
        var endUtc = startUtc.AddDays(1);
        return (new DateTimeOffset(startUtc, TimeSpan.Zero), new DateTimeOffset(endUtc, TimeSpan.Zero));
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static StaffAttendanceEventResponse ToResponse(StaffAttendanceEvent entity) =>
        new(
            entity.Id,
            entity.EventType,
            entity.OccurredAt,
            entity.BusinessLocationId,
            entity.LocationVerificationStatus,
            entity.VerificationMethod);

    private async Task WriteAuditAsync(Guid tenantId, StaffAttendanceEvent entity, CancellationToken cancellationToken)
    {
        var action = entity.EventType switch
        {
            StaffAttendanceEventTypes.ClockIn => AuditActions.StaffClockIn,
            StaffAttendanceEventTypes.BreakStart => AuditActions.StaffBreakStart,
            StaffAttendanceEventTypes.BreakEnd => AuditActions.StaffBreakEnd,
            StaffAttendanceEventTypes.ClockOut => AuditActions.StaffClockOut,
            _ => null,
        };

        if (action is null)
        {
            return;
        }

        var actorId = currentUser.UserId ?? "portal:unknown";
        await auditLogWriter.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actorId,
                action,
                "staff_attendance",
                entity.Id.ToString(),
                correlationContext.CorrelationId,
                clock.GetUtcNow(),
                $"{{\"tenantId\":\"{tenantId}\",\"staffMemberId\":\"{entity.StaffMemberId}\",\"locationId\":\"{entity.BusinessLocationId}\"}}"),
            cancellationToken);
    }

    private async Task WriteOverrideAuditAsync(
        Guid tenantId,
        StaffAttendanceEvent entity,
        CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? "portal:unknown";
        await auditLogWriter.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actorId,
                AuditActions.StaffAttendanceOverride,
                "staff_attendance",
                entity.Id.ToString(),
                correlationContext.CorrelationId,
                clock.GetUtcNow(),
                $"{{\"tenantId\":\"{tenantId}\",\"staffMemberId\":\"{entity.StaffMemberId}\",\"eventType\":\"{entity.EventType}\"}}"),
            cancellationToken);
    }

    private async Task WriteCorrectionAuditAsync(
        Guid tenantId,
        StaffAttendanceEvent entity,
        CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? "portal:unknown";
        await auditLogWriter.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actorId,
                AuditActions.StaffAttendanceCorrection,
                "staff_attendance",
                entity.Id.ToString(),
                correlationContext.CorrelationId,
                clock.GetUtcNow(),
                $"{{\"tenantId\":\"{tenantId}\",\"staffMemberId\":\"{entity.StaffMemberId}\",\"relatedEventId\":\"{entity.RelatedEventId}\"}}"),
            cancellationToken);
    }
}
