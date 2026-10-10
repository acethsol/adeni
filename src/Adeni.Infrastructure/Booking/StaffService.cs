namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Caching;
using Adeni.Application.Storage;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class StaffService(
    AdeniDbContext dbContext,
    IFileStorage fileStorage,
    ICacheService cache) : IStaffService
{
    public async Task<IReadOnlyList<StaffMemberResponse>> ListForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var staff = await dbContext.StaffMembers
            .AsNoTracking()
            .Include(x => x.ServiceLinks)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        var staffIds = staff.Select(x => x.Id).ToArray();
        var now = DateTimeOffset.UtcNow;

        var linkedUsers = await dbContext.BusinessUsers
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.StaffMemberId != null && staffIds.Contains(x.StaffMemberId.Value))
            .Select(x => new { x.Id, x.StaffMemberId, x.Role })
            .ToListAsync(cancellationToken);

        var pendingInvites = await dbContext.StaffPortalInvites
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.StaffMemberId != null
                && staffIds.Contains(x.StaffMemberId.Value)
                && x.Status == StaffPortalInviteStatuses.Pending
                && x.ExpiresAt >= now)
            .Select(x => new { x.Id, x.StaffMemberId })
            .ToListAsync(cancellationToken);

        var userByStaff = linkedUsers
            .Where(x => x.StaffMemberId is not null)
            .GroupBy(x => x.StaffMemberId!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        var inviteByStaff = pendingInvites
            .Where(x => x.StaffMemberId is not null)
            .GroupBy(x => x.StaffMemberId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var responses = new List<StaffMemberResponse>(staff.Count);
        foreach (var member in staff)
        {
            string accessStatus = PortalAccessStatuses.None;
            Guid? inviteId = null;
            Guid? businessUserId = null;
            string? permissionRole = null;

            if (userByStaff.TryGetValue(member.Id, out var user))
            {
                accessStatus = PortalAccessStatuses.Active;
                businessUserId = user.Id;
                permissionRole = PortalPermissionRoles.Normalize(user.Role);
            }
            else if (inviteByStaff.TryGetValue(member.Id, out var invite))
            {
                accessStatus = PortalAccessStatuses.InvitePending;
                inviteId = invite.Id;
            }

            responses.Add(await ToResponseAsync(
                member,
                cancellationToken,
                accessStatus,
                inviteId,
                businessUserId,
                permissionRole));
        }

        return responses;
    }

    public async Task<Result<IReadOnlyList<PublicStaffMemberResponse>>> ListPublicBySlugAsync(
        string slug,
        Guid? serviceOfferingId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<IReadOnlyList<PublicStaffMemberResponse>>(
                Error.Validation("Business slug is required."));
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var match = await VerifiedLocationQueries.ResolveLocationBySlugAsync(
            dbContext,
            normalizedSlug,
            cancellationToken);

        if (match is null)
        {
            return Result.Failure<IReadOnlyList<PublicStaffMemberResponse>>(Error.NotFound("Business"));
        }

        var query = dbContext.StaffMembers
            .AsNoTracking()
            .Include(x => x.ServiceLinks)
            .Where(x => x.TenantId == match.Value.TenantId && x.IsActive);

        if (serviceOfferingId is Guid serviceId)
        {
            var serviceExists = await dbContext.ServiceOfferings
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == serviceId && x.TenantId == match.Value.TenantId && x.IsActive,
                    cancellationToken);

            if (!serviceExists)
            {
                return Result.Failure<IReadOnlyList<PublicStaffMemberResponse>>(Error.NotFound("Service"));
            }

            query = query.Where(x =>
                !x.ServiceLinks.Any()
                || x.ServiceLinks.Any(link => link.ServiceOfferingId == serviceId));
        }

        var staff = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        var responses = new List<PublicStaffMemberResponse>(staff.Count);
        foreach (var member in staff)
        {
            responses.Add(await ToPublicResponseAsync(member, cancellationToken));
        }

        return Result.Success<IReadOnlyList<PublicStaffMemberResponse>>(responses);
    }

    public async Task<Result<StaffMemberResponse>> CreateAsync(
        Guid tenantId,
        CreateStaffMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var identity = ValidateIdentity(
            request.FirstName,
            request.LastName,
            request.DisplayName,
            request.RoleKey,
            request.Title,
            request.Bio);
        if (identity.IsFailure)
        {
            return Result.Failure<StaffMemberResponse>(identity.Error);
        }

        if (!await dbContext.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            return Result.Failure<StaffMemberResponse>(Error.NotFound("Tenant"));
        }

        var serviceIds = request.ServiceOfferingIds ?? Array.Empty<Guid>();
        var serviceCheck = await ValidateServiceIdsAsync(tenantId, serviceIds, cancellationToken);
        if (serviceCheck.IsFailure)
        {
            return Result.Failure<StaffMemberResponse>(serviceCheck.Error);
        }

        var (firstName, lastName, displayName, roleKey, title, bio) = identity.Value!;
        var now = DateTimeOffset.UtcNow;
        var entity = new StaffMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = firstName,
            LastName = lastName,
            DisplayName = displayName,
            RoleKey = roleKey,
            Title = title,
            Bio = bio,
            IsActive = true,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var serviceId in serviceIds.Distinct())
        {
            entity.ServiceLinks.Add(new StaffServiceLink
            {
                StaffMemberId = entity.Id,
                ServiceOfferingId = serviceId,
                TenantId = tenantId,
            });
        }

        dbContext.StaffMembers.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicStaffCacheAsync(tenantId, cancellationToken);

        return Result.Success(await ToResponseAsync(entity, cancellationToken));
    }

    public async Task<Result<StaffMemberResponse>> UpdateAsync(
        Guid tenantId,
        Guid staffMemberId,
        UpdateStaffMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var identity = ValidateIdentity(
            request.FirstName,
            request.LastName,
            request.DisplayName,
            request.RoleKey,
            request.Title,
            request.Bio);
        if (identity.IsFailure)
        {
            return Result.Failure<StaffMemberResponse>(identity.Error);
        }

        var entity = await dbContext.StaffMembers
            .Include(x => x.ServiceLinks)
            .FirstOrDefaultAsync(x => x.Id == staffMemberId && x.TenantId == tenantId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure<StaffMemberResponse>(Error.NotFound("Staff member"));
        }

        var (firstName, lastName, displayName, roleKey, title, bio) = identity.Value!;
        entity.FirstName = firstName;
        entity.LastName = lastName;
        entity.DisplayName = displayName;
        entity.RoleKey = roleKey;
        entity.Title = title;
        entity.Bio = bio;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicStaffCacheAsync(tenantId, cancellationToken);

        return Result.Success(await ToResponseAsync(entity, cancellationToken));
    }

    public async Task<Result> DeactivateAsync(
        Guid tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.StaffMembers
            .FirstOrDefaultAsync(x => x.Id == staffMemberId && x.TenantId == tenantId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure(Error.NotFound("Staff member"));
        }

        entity.IsActive = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicStaffCacheAsync(tenantId, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<StaffMemberResponse>> ReplaceServicesAsync(
        Guid tenantId,
        Guid staffMemberId,
        IReadOnlyList<Guid> serviceOfferingIds,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.StaffMembers
            .Include(x => x.ServiceLinks)
            .FirstOrDefaultAsync(x => x.Id == staffMemberId && x.TenantId == tenantId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure<StaffMemberResponse>(Error.NotFound("Staff member"));
        }

        var ids = serviceOfferingIds ?? Array.Empty<Guid>();
        var serviceCheck = await ValidateServiceIdsAsync(tenantId, ids, cancellationToken);
        if (serviceCheck.IsFailure)
        {
            return Result.Failure<StaffMemberResponse>(serviceCheck.Error);
        }

        dbContext.StaffServiceLinks.RemoveRange(entity.ServiceLinks);
        entity.ServiceLinks.Clear();

        foreach (var serviceId in ids.Distinct())
        {
            entity.ServiceLinks.Add(new StaffServiceLink
            {
                StaffMemberId = entity.Id,
                ServiceOfferingId = serviceId,
                TenantId = tenantId,
            });
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicStaffCacheAsync(tenantId, cancellationToken);

        return Result.Success(await ToResponseAsync(entity, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<WeeklyAvailabilityRule>>> GetHoursAsync(
        Guid tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken = default)
    {
        if (!await StaffExistsAsync(tenantId, staffMemberId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<WeeklyAvailabilityRule>>(Error.NotFound("Staff member"));
        }

        var rules = await dbContext.StaffWeeklyAvailabilities
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.StaffMemberId == staffMemberId)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.OpenTime)
            .Select(x => new WeeklyAvailabilityRule(x.DayOfWeek, x.OpenTime, x.CloseTime))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<WeeklyAvailabilityRule>>(rules);
    }

    public async Task<Result<IReadOnlyList<WeeklyAvailabilityRule>>> ReplaceHoursAsync(
        Guid tenantId,
        Guid staffMemberId,
        IReadOnlyList<WeeklyAvailabilityRule> rules,
        CancellationToken cancellationToken = default)
    {
        if (!await StaffExistsAsync(tenantId, staffMemberId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<WeeklyAvailabilityRule>>(Error.NotFound("Staff member"));
        }

        var validation = ValidateHours(rules);
        if (validation.IsFailure)
        {
            return Result.Failure<IReadOnlyList<WeeklyAvailabilityRule>>(validation.Error);
        }

        var existing = await dbContext.StaffWeeklyAvailabilities
            .Where(x => x.TenantId == tenantId && x.StaffMemberId == staffMemberId)
            .ToListAsync(cancellationToken);
        dbContext.StaffWeeklyAvailabilities.RemoveRange(existing);

        foreach (var rule in rules)
        {
            dbContext.StaffWeeklyAvailabilities.Add(new StaffWeeklyAvailability
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                StaffMemberId = staffMemberId,
                DayOfWeek = rule.DayOfWeek,
                OpenTime = rule.OpenTime,
                CloseTime = rule.CloseTime,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicStaffCacheAsync(tenantId, cancellationToken);
        return Result.Success(rules);
    }

    public async Task<Result<IReadOnlyList<StaffLeaveResponse>>> ListLeaveAsync(
        Guid tenantId,
        Guid staffMemberId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        if (!await StaffExistsAsync(tenantId, staffMemberId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<StaffLeaveResponse>>(Error.NotFound("Staff member"));
        }

        var query = dbContext.StaffLeaves
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.StaffMemberId == staffMemberId);

        if (from is DateTimeOffset fromAt)
        {
            query = query.Where(x => x.EndAt > fromAt);
        }

        if (to is DateTimeOffset toAt)
        {
            query = query.Where(x => x.StartAt < toAt);
        }

        var leave = await query.OrderBy(x => x.StartAt).ToListAsync(cancellationToken);
        var bookingConflicts = await LoadConflictingBookingIdsAsync(
            tenantId,
            staffMemberId,
            leave,
            cancellationToken);

        return Result.Success<IReadOnlyList<StaffLeaveResponse>>(
            leave.Select(x => ToLeaveResponse(x, bookingConflicts)).ToArray());
    }

    public async Task<Result<StaffLeaveResponse>> CreateLeaveAsync(
        Guid tenantId,
        Guid staffMemberId,
        CreateStaffLeaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await StaffExistsAsync(tenantId, staffMemberId, cancellationToken))
        {
            return Result.Failure<StaffLeaveResponse>(Error.NotFound("Staff member"));
        }

        var start = request.StartAt.ToUniversalTime();
        var end = request.EndAt.ToUniversalTime();
        if (end <= start)
        {
            return Result.Failure<StaffLeaveResponse>(
                Error.Validation("Leave end must be after start."));
        }

        var overlapsExistingLeave = await dbContext.StaffLeaves
            .AsNoTracking()
            .AnyAsync(
                x => x.TenantId == tenantId
                    && x.StaffMemberId == staffMemberId
                    && x.StartAt < end
                    && x.EndAt > start,
                cancellationToken);

        if (overlapsExistingLeave)
        {
            return Result.Failure<StaffLeaveResponse>(ErrorCodes.StaffLeaveOverlapError());
        }

        var entity = new StaffLeave
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StaffMemberId = staffMemberId,
            StartAt = start,
            EndAt = end,
            Reason = NormalizeOptional(request.Reason, 200),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        dbContext.StaffLeaves.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicStaffCacheAsync(tenantId, cancellationToken);

        var conflicts = await LoadConflictingBookingIdsAsync(
            tenantId,
            staffMemberId,
            [entity],
            cancellationToken);

        return Result.Success(ToLeaveResponse(entity, conflicts));
    }

    public async Task<Result> DeleteLeaveAsync(
        Guid tenantId,
        Guid staffMemberId,
        Guid leaveId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.StaffLeaves
            .FirstOrDefaultAsync(
                x => x.Id == leaveId && x.StaffMemberId == staffMemberId && x.TenantId == tenantId,
                cancellationToken);

        if (entity is null)
        {
            return Result.Failure(ErrorCodes.StaffLeaveNotFoundError());
        }

        dbContext.StaffLeaves.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicStaffCacheAsync(tenantId, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<StaffCalendarResponse>> GetCalendarAsync(
        Guid tenantId,
        Guid staffMemberId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        if (to <= from)
        {
            return Result.Failure<StaffCalendarResponse>(
                Error.Validation("Calendar range end must be after start."));
        }

        if ((to - from).TotalDays > 45)
        {
            return Result.Failure<StaffCalendarResponse>(
                Error.Validation("Calendar range cannot exceed 45 days."));
        }

        var member = await dbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == staffMemberId && x.TenantId == tenantId, cancellationToken);

        if (member is null)
        {
            return Result.Failure<StaffCalendarResponse>(Error.NotFound("Staff member"));
        }

        var hoursResult = await GetHoursAsync(tenantId, staffMemberId, cancellationToken);
        if (hoursResult.IsFailure)
        {
            return Result.Failure<StaffCalendarResponse>(hoursResult.Error);
        }

        var fromUtc = from.ToUniversalTime();
        var toUtc = to.ToUniversalTime();

        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x =>
                x.TenantId == tenantId
                && x.StaffMemberId == staffMemberId
                && x.StartAt < toUtc
                && x.EndAt > fromUtc
                && (x.Status == BookingStatus.Pending || x.Status == BookingStatus.Confirmed))
            .OrderBy(x => x.StartAt)
            .ToListAsync(cancellationToken);

        var leave = await dbContext.StaffLeaves
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.StaffMemberId == staffMemberId
                && x.StartAt < toUtc
                && x.EndAt > fromUtc)
            .OrderBy(x => x.StartAt)
            .ToListAsync(cancellationToken);

        var serviceIds = bookings.Select(x => x.ServiceOfferingId).Distinct().ToArray();
        var serviceNames = await dbContext.ServiceOfferings
            .AsNoTracking()
            .Where(x => serviceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return Result.Success(new StaffCalendarResponse(
            member.Id,
            member.DisplayName,
            hoursResult.Value!,
            hoursResult.Value!.Count == 0,
            bookings.Select(b => new StaffCalendarBookingItem(
                b.Id,
                b.Lines.OrderBy(l => l.SortOrder).FirstOrDefault()?.ServiceName
                    ?? serviceNames.GetValueOrDefault(b.ServiceOfferingId, "Service"),
                b.StartAt,
                b.EndAt,
                (int)b.Status,
                b.CustomerNotes)).ToArray(),
            leave.Select(l => new StaffCalendarLeaveItem(l.Id, l.StartAt, l.EndAt, l.Reason)).ToArray()));
    }

    private async Task<bool> StaffExistsAsync(
        Guid tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken) =>
        await dbContext.StaffMembers
            .AsNoTracking()
            .AnyAsync(x => x.Id == staffMemberId && x.TenantId == tenantId, cancellationToken);

    private async Task<Dictionary<Guid, IReadOnlyList<Guid>>> LoadConflictingBookingIdsAsync(
        Guid tenantId,
        Guid staffMemberId,
        IReadOnlyList<StaffLeave> leaveEntries,
        CancellationToken cancellationToken)
    {
        if (leaveEntries.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Guid>>();
        }

        var min = leaveEntries.Min(x => x.StartAt);
        var max = leaveEntries.Max(x => x.EndAt);
        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.StaffMemberId == staffMemberId
                && x.StartAt < max
                && x.EndAt > min
                && (x.Status == BookingStatus.Pending || x.Status == BookingStatus.Confirmed))
            .Select(x => new { x.Id, x.StartAt, x.EndAt })
            .ToListAsync(cancellationToken);

        return leaveEntries.ToDictionary(
            leave => leave.Id,
            leave => (IReadOnlyList<Guid>)bookings
                .Where(b => b.StartAt < leave.EndAt && b.EndAt > leave.StartAt)
                .Select(b => b.Id)
                .ToArray());
    }

    private static StaffLeaveResponse ToLeaveResponse(
        StaffLeave entity,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> conflicts) =>
        new(
            entity.Id,
            entity.StaffMemberId,
            entity.StartAt,
            entity.EndAt,
            entity.Reason,
            entity.CreatedAt,
            conflicts.TryGetValue(entity.Id, out var ids) ? ids : Array.Empty<Guid>());

    private async Task<Result> ValidateServiceIdsAsync(
        Guid tenantId,
        IReadOnlyList<Guid> serviceIds,
        CancellationToken cancellationToken)
    {
        if (serviceIds.Count == 0)
        {
            return Result.Success();
        }

        var distinct = serviceIds.Distinct().ToArray();
        var found = await dbContext.ServiceOfferings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && distinct.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (found.Count != distinct.Length)
        {
            return Result.Failure(Error.Validation("One or more services were not found for this business."));
        }

        return Result.Success();
    }

    private async Task InvalidatePublicStaffCacheAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var slugs = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => x.Slug)
            .ToListAsync(cancellationToken);

        foreach (var slug in slugs)
        {
            await cache.RemoveAsync(CacheKeys.PublicStaff(slug), cancellationToken);
        }
    }

    private async Task<StaffMemberResponse> ToResponseAsync(
        StaffMember entity,
        CancellationToken cancellationToken,
        string portalAccessStatus = PortalAccessStatuses.None,
        Guid? portalInviteId = null,
        Guid? businessUserId = null,
        string? permissionRole = null)
    {
        string? avatarUrl = null;
        if (!string.IsNullOrWhiteSpace(entity.AvatarImageKey))
        {
            avatarUrl = await fileStorage.GetDownloadUrlAsync(entity.AvatarImageKey, cancellationToken);
        }

        return new StaffMemberResponse(
            entity.Id,
            entity.FirstName,
            entity.LastName,
            entity.DisplayName,
            entity.RoleKey,
            entity.Title,
            entity.Bio,
            entity.IsActive,
            entity.SortOrder,
            avatarUrl,
            entity.ServiceLinks.Select(x => x.ServiceOfferingId).ToArray(),
            portalAccessStatus,
            portalInviteId,
            businessUserId,
            permissionRole);
    }

    private async Task<PublicStaffMemberResponse> ToPublicResponseAsync(
        StaffMember entity,
        CancellationToken cancellationToken)
    {
        string? avatarUrl = null;
        if (!string.IsNullOrWhiteSpace(entity.AvatarImageKey))
        {
            avatarUrl = await fileStorage.GetDownloadUrlAsync(entity.AvatarImageKey, cancellationToken);
        }

        return new PublicStaffMemberResponse(
            entity.Id,
            entity.DisplayName,
            entity.RoleKey,
            entity.Title,
            entity.Bio,
            entity.SortOrder,
            avatarUrl,
            entity.ServiceLinks.Select(x => x.ServiceOfferingId).ToArray());
    }

    private static Result<(string FirstName, string LastName, string DisplayName, string RoleKey, string? Title, string? Bio)>
        ValidateIdentity(
            string firstName,
            string lastName,
            string? displayName,
            string? roleKey,
            string? title,
            string? bio)
    {
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Trim().Length < 1)
        {
            return Result.Failure<(string, string, string, string, string?, string?)>(
                Error.Validation("First name is required."));
        }

        if (string.IsNullOrWhiteSpace(lastName) || lastName.Trim().Length < 1)
        {
            return Result.Failure<(string, string, string, string, string?, string?)>(
                Error.Validation("Last name is required."));
        }

        var first = firstName.Trim();
        var last = lastName.Trim();
        if (first.Length > 80)
        {
            return Result.Failure<(string, string, string, string, string?, string?)>(
                Error.Validation("First name must be at most 80 characters."));
        }

        if (last.Length > 80)
        {
            return Result.Failure<(string, string, string, string, string?, string?)>(
                Error.Validation("Last name must be at most 80 characters."));
        }

        var display = string.IsNullOrWhiteSpace(displayName)
            ? $"{first} {last}".Trim()
            : displayName.Trim();

        if (display.Length < 2 || display.Length > 120)
        {
            return Result.Failure<(string, string, string, string, string?, string?)>(
                Error.Validation("Display name must be between 2 and 120 characters."));
        }

        if (title is { Length: > 120 })
        {
            return Result.Failure<(string, string, string, string, string?, string?)>(
                Error.Validation("Title must be at most 120 characters."));
        }

        if (bio is { Length: > 500 })
        {
            return Result.Failure<(string, string, string, string, string?, string?)>(
                Error.Validation("Bio must be at most 500 characters."));
        }

        if (roleKey is not null && !StaffRoleKeys.IsValid(roleKey))
        {
            return Result.Failure<(string, string, string, string, string?, string?)>(
                Error.Validation("Role is not recognized."));
        }

        return Result.Success((
            first,
            last,
            display,
            StaffRoleKeys.Normalize(roleKey),
            NormalizeOptional(title, 120),
            NormalizeOptional(bio, 500)));
    }

    private static Result ValidateHours(IReadOnlyList<WeeklyAvailabilityRule> rules)
    {
        foreach (var rule in rules)
        {
            if (rule.CloseTime <= rule.OpenTime)
            {
                return Result.Failure(ErrorCodes.StaffHoursInvalidError("Close time must be after open time."));
            }
        }

        return Result.Success();
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
