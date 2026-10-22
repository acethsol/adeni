namespace Adeni.Infrastructure.Booking;

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Adeni.Application.Booking;
using Adeni.Application.Catalog;
using Adeni.Application.Notifications;
using Adeni.Application.Realtime;
using Adeni.Application.Tenancy;
using Adeni.Infrastructure.Realtime;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class QueueService(
    AdeniDbContext dbContext,
    IBusinessCapabilitiesService capabilities,
    INotificationJobScheduler notificationJobs,
    ITenantCustomerService tenantCustomers,
    ITenantRealtimePublisher tenantRealtime) : IQueueService
{
    private static readonly Regex PhoneDigits = new(@"[^\d+]", RegexOptions.Compiled);

    public async Task<Result<QueueTicketResponse>> JoinBySlugAsync(
        string tenantSlug,
        JoinQueueRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await ResolveTenantIdBySlugAsync(tenantSlug, cancellationToken);
        if (tenantId is null)
        {
            return Result.Failure<QueueTicketResponse>(Error.NotFound("Business"));
        }

        var capability = await EnsureQueueCapabilityAsync(tenantId.Value, cancellationToken);
        if (capability.IsFailure)
        {
            return Result.Failure<QueueTicketResponse>(capability.Error);
        }

        return await CreateTicketAsync(
            tenantId.Value,
            request.DisplayName,
            request.Phone,
            request.Email,
            request.ServiceOfferingId,
            staffMemberId: null,
            request.PartySize,
            request.Notes,
            cancellationToken);
    }

    public async Task<Result<QueueTicketResponse>> GetByPublicTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure<QueueTicketResponse>(Error.NotFound("Queue ticket"));
        }

        var ticket = await dbContext.QueueTickets
            .AsNoTracking()
            .Include(x => x.ServiceOffering)
            .Include(x => x.StaffMember)
            .FirstOrDefaultAsync(x => x.PublicToken == token.Trim(), cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<QueueTicketResponse>(Error.NotFound("Queue ticket"));
        }

        var businessName = await dbContext.Tenants.AsNoTracking()
            .Where(x => x.Id == ticket.TenantId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var mapped = await MapAsync(ticket, cancellationToken);
        return Result.Success(mapped with { BusinessName = businessName });
    }

    public async Task<Result<IReadOnlyList<QueueTicketResponse>>> ListForTenantAsync(
        Guid tenantId,
        Guid? requireStaffMemberId = null,
        bool includeFinishedToday = true,
        CancellationToken cancellationToken = default)
    {
        var capability = await EnsureQueueCapabilityAsync(tenantId, cancellationToken);
        if (capability.IsFailure)
        {
            return Result.Failure<IReadOnlyList<QueueTicketResponse>>(capability.Error);
        }

        var today = DateTimeOffset.UtcNow.Date;
        var query = dbContext.QueueTickets
            .AsNoTracking()
            .Include(x => x.ServiceOffering)
            .Include(x => x.StaffMember)
            .Where(x => x.TenantId == tenantId);

        if (requireStaffMemberId is Guid staffId)
        {
            query = query.Where(x => x.StaffMemberId == null || x.StaffMemberId == staffId);
        }

        if (includeFinishedToday)
        {
            query = query.Where(x =>
                x.Status == QueueTicketStatus.Waiting
                || x.Status == QueueTicketStatus.Called
                || x.Status == QueueTicketStatus.InService
                || (x.JoinedAt >= today
                    && (x.Status == QueueTicketStatus.Done
                        || x.Status == QueueTicketStatus.NoShow
                        || x.Status == QueueTicketStatus.Cancelled)));
        }
        else
        {
            query = query.Where(x =>
                x.Status == QueueTicketStatus.Waiting
                || x.Status == QueueTicketStatus.Called
                || x.Status == QueueTicketStatus.InService);
        }

        var tickets = await query
            .OrderBy(x => x.Status)
            .ThenBy(x => x.JoinedAt)
            .ToListAsync(cancellationToken);

        var items = new List<QueueTicketResponse>();
        foreach (var ticket in tickets)
        {
            items.Add(await MapAsync(ticket, cancellationToken));
        }

        return Result.Success<IReadOnlyList<QueueTicketResponse>>(items);
    }

    public async Task<Result<TenantQueueFloorResponse>> GetFloorForTenantAsync(
        Guid tenantId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default)
    {
        var list = await ListForTenantAsync(tenantId, requireStaffMemberId, cancellationToken: cancellationToken);
        if (list.IsFailure)
        {
            return Result.Failure<TenantQueueFloorResponse>(list.Error);
        }

        var profile = await dbContext.BusinessProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<TenantQueueFloorResponse>(Error.NotFound("Business"));
        }

        var now = DateTimeOffset.UtcNow;
        var expected = await LoadExpectedArrivalsAsync(tenantId, requireStaffMemberId, now, cancellationToken);
        var status = MapWalkInStatus(profile, now);

        return Result.Success(new TenantQueueFloorResponse(list.Value!, expected, status));
    }

    public async Task<Result> UpsertTicketFromBookingCheckInAsync(
        Guid tenantId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var capability = await EnsureQueueCapabilityAsync(tenantId, cancellationToken);
        if (capability.IsFailure)
        {
            return Result.Success();
        }

        var booking = await dbContext.Bookings.AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.TenantId == tenantId, cancellationToken);
        if (booking is null || booking.Status != BookingStatus.CheckedIn)
        {
            return Result.Success();
        }

        var existing = await dbContext.QueueTickets
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                    && x.BookingId == bookingId
                    && x.Status != QueueTicketStatus.Cancelled,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.Status is QueueTicketStatus.Done or QueueTicketStatus.NoShow)
            {
                return Result.Success();
            }

            return Result.Success();
        }

        var customer = await dbContext.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == booking.CustomerId, cancellationToken);

        var name = string.IsNullOrWhiteSpace(customer?.Name) ? "Guest" : customer!.Name;
        var phone = customer?.Phone ?? string.Empty;
        if (string.IsNullOrWhiteSpace(NormalizePhone(phone)))
        {
            phone = "+0000000000";
        }

        var customerResult = await tenantCustomers.EnsureFromBookingAsync(tenantId, booking.CustomerId, cancellationToken);
        Guid? tenantCustomerId = customerResult.IsSuccess ? customerResult.Value!.TenantCustomerId : null;

        var now = DateTimeOffset.UtcNow;
        var ticket = new QueueTicket
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceOfferingId = booking.ServiceOfferingId,
            StaffMemberId = booking.StaffMemberId,
            CustomerId = booking.CustomerId,
            TenantCustomerId = tenantCustomerId,
            GuestName = name,
            GuestPhone = NormalizePhone(phone),
            Status = QueueTicketStatus.Waiting,
            PartySize = Math.Clamp(booking.GuestCount, 1, ErrorCodes.MaxBookingGuests),
            Notes = "Appointment check-in",
            PublicToken = CreatePublicToken(),
            BookingId = booking.Id,
            Source = QueueTicketSource.Appointment,
            JoinedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.QueueTickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);
        await TenantRealtimeNotifications.PublishQueueFloorAsync(tenantId, tenantRealtime, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<WalkInSettingsResponse>> UpdateWalkInSettingsAsync(
        Guid tenantId,
        UpdateWalkInSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var capability = await EnsureQueueCapabilityAsync(tenantId, cancellationToken);
        if (capability.IsFailure)
        {
            return Result.Failure<WalkInSettingsResponse>(capability.Error);
        }

        var profile = await dbContext.BusinessProfiles
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<WalkInSettingsResponse>(Error.NotFound("Business"));
        }

        profile.WalkInsEnabled = request.WalkInsEnabled;
        profile.MaxWalkInWaiting = request.MaxWalkInWaiting is > 0 ? request.MaxWalkInWaiting : null;
        profile.WalkInCalendarPolicy = WalkInQueuePolicy.FromApiPolicy(request.CalendarPolicy);
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        return Result.Success(ToWalkInSettingsResponse(profile, now));
    }

    public async Task<Result<WalkInSettingsResponse>> OpenWalkInBlockAsync(
        Guid tenantId,
        OpenWalkInBlockRequest request,
        CancellationToken cancellationToken = default)
    {
        var capability = await EnsureQueueCapabilityAsync(tenantId, cancellationToken);
        if (capability.IsFailure)
        {
            return Result.Failure<WalkInSettingsResponse>(capability.Error);
        }

        var minutes = Math.Clamp(request.DurationMinutes, 15, 480);
        var profile = await dbContext.BusinessProfiles
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<WalkInSettingsResponse>(Error.NotFound("Business"));
        }

        var now = DateTimeOffset.UtcNow;
        profile.WalkInsEnabled = true;
        profile.WalkInAcceptingUntil = now.AddMinutes(minutes);
        profile.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToWalkInSettingsResponse(profile, now));
    }

    public async Task<Result<QueueTicketResponse>> AddForTenantAsync(
        Guid tenantId,
        StaffAddQueueRequest request,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default)
    {
        var capability = await EnsureQueueCapabilityAsync(tenantId, cancellationToken);
        if (capability.IsFailure)
        {
            return Result.Failure<QueueTicketResponse>(capability.Error);
        }

        var staffId = request.StaffMemberId ?? requireStaffMemberId;
        if (requireStaffMemberId is Guid required
            && staffId is Guid assigned
            && assigned != required)
        {
            return Result.Failure<QueueTicketResponse>(ErrorCodes.PermissionDeniedError());
        }

        return await CreateTicketAsync(
            tenantId,
            request.DisplayName,
            request.Phone,
            request.Email,
            request.ServiceOfferingId,
            staffId,
            request.PartySize,
            request.Notes,
            cancellationToken);
    }

    public Task<Result<QueueTicketResponse>> CallAsync(
        Guid tenantId,
        Guid ticketId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            tenantId,
            ticketId,
            QueueTicketStatus.Called,
            requireStaffMemberId,
            request: null,
            cancellationToken);

    public Task<Result<QueueTicketResponse>> StartAsync(
        Guid tenantId,
        Guid ticketId,
        StartQueueTicketRequest? request = null,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            tenantId,
            ticketId,
            QueueTicketStatus.InService,
            requireStaffMemberId,
            request,
            cancellationToken);

    public Task<Result<QueueTicketResponse>> CompleteAsync(
        Guid tenantId,
        Guid ticketId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            tenantId,
            ticketId,
            QueueTicketStatus.Done,
            requireStaffMemberId,
            request: null,
            cancellationToken);

    public Task<Result<QueueTicketResponse>> MarkNoShowAsync(
        Guid tenantId,
        Guid ticketId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            tenantId,
            ticketId,
            QueueTicketStatus.NoShow,
            requireStaffMemberId,
            request: null,
            cancellationToken);

    public Task<Result<QueueTicketResponse>> CancelAsync(
        Guid tenantId,
        Guid ticketId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            tenantId,
            ticketId,
            QueueTicketStatus.Cancelled,
            requireStaffMemberId,
            request: null,
            cancellationToken);

    private async Task<Result<QueueTicketResponse>> TransitionAsync(
        Guid tenantId,
        Guid ticketId,
        QueueTicketStatus target,
        Guid? requireStaffMemberId,
        StartQueueTicketRequest? request,
        CancellationToken cancellationToken)
    {
        var capability = await EnsureQueueCapabilityAsync(tenantId, cancellationToken);
        if (capability.IsFailure)
        {
            return Result.Failure<QueueTicketResponse>(capability.Error);
        }

        var ticket = await dbContext.QueueTickets
            .Include(x => x.ServiceOffering)
            .Include(x => x.StaffMember)
            .FirstOrDefaultAsync(x => x.Id == ticketId && x.TenantId == tenantId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<QueueTicketResponse>(Error.NotFound("Queue ticket"));
        }

        if (requireStaffMemberId is Guid requiredStaff
            && ticket.StaffMemberId is Guid assigned
            && assigned != requiredStaff)
        {
            return Result.Failure<QueueTicketResponse>(ErrorCodes.PermissionDeniedError());
        }

        var now = DateTimeOffset.UtcNow;
        switch (target)
        {
            case QueueTicketStatus.Called when ticket.Status != QueueTicketStatus.Waiting:
                return Result.Failure<QueueTicketResponse>(
                    Error.Conflict("Only waiting guests can be called."));
            case QueueTicketStatus.InService when ticket.Status is not (QueueTicketStatus.Called or QueueTicketStatus.Waiting):
                return Result.Failure<QueueTicketResponse>(
                    Error.Conflict("Only called or waiting guests can start service."));
            case QueueTicketStatus.Done when ticket.Status != QueueTicketStatus.InService:
                return Result.Failure<QueueTicketResponse>(
                    Error.Conflict("Only in-service guests can be completed."));
            case QueueTicketStatus.NoShow when ticket.Status is not (QueueTicketStatus.Waiting or QueueTicketStatus.Called):
                return Result.Failure<QueueTicketResponse>(
                    Error.Conflict("Only waiting or called guests can be marked no-show."));
            case QueueTicketStatus.Cancelled when ticket.Status != QueueTicketStatus.Waiting:
                return Result.Failure<QueueTicketResponse>(
                    Error.Conflict("Only waiting guests can cancel."));
        }

        ticket.Status = target;
        ticket.UpdatedAt = now;

        switch (target)
        {
            case QueueTicketStatus.Called:
                ticket.CalledAt = now;
                await NotifyCalledAsync(ticket, cancellationToken);
                break;
            case QueueTicketStatus.InService:
                if (request?.StaffMemberId is Guid startStaff)
                {
                    if (requireStaffMemberId is Guid rs && startStaff != rs)
                    {
                        return Result.Failure<QueueTicketResponse>(ErrorCodes.PermissionDeniedError());
                    }

                    ticket.StaffMemberId = startStaff;
                }
                else if (requireStaffMemberId is Guid autoStaff && ticket.StaffMemberId is null)
                {
                    ticket.StaffMemberId = autoStaff;
                }

                var startConflict = await ValidateStartAgainstCalendarAsync(ticket, cancellationToken);
                if (startConflict.IsFailure)
                {
                    return Result.Failure<QueueTicketResponse>(startConflict.Error);
                }

                ticket.StartedAt = now;

                if (ticket.ServiceOfferingId is not null && ticket.BookingId is null)
                {
                    await CreateLinkedBookingAsync(ticket, cancellationToken);
                }

                break;
            case QueueTicketStatus.Done:
                ticket.CompletedAt = now;
                if (ticket.BookingId is Guid bookingId)
                {
                    var booking = await dbContext.Bookings
                        .FirstOrDefaultAsync(x => x.Id == bookingId && x.TenantId == tenantId, cancellationToken);
                    if (booking is not null
                        && booking.Status is BookingStatus.Confirmed or BookingStatus.CheckedIn)
                    {
                        booking.Status = BookingStatus.Completed;
                        booking.UpdatedAt = now;
                    }
                }

                break;
            case QueueTicketStatus.NoShow:
                ticket.NoShowAt = now;
                break;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await TenantRealtimeNotifications.PublishQueueFloorAsync(ticket.TenantId, tenantRealtime, cancellationToken);

        // reload navs
        await dbContext.Entry(ticket).Reference(x => x.ServiceOffering).LoadAsync(cancellationToken);
        await dbContext.Entry(ticket).Reference(x => x.StaffMember).LoadAsync(cancellationToken);
        return Result.Success(await MapAsync(ticket, cancellationToken));
    }

    private async Task CreateLinkedBookingAsync(QueueTicket ticket, CancellationToken cancellationToken)
    {
        var service = ticket.ServiceOffering
            ?? await dbContext.ServiceOfferings
                .FirstOrDefaultAsync(x => x.Id == ticket.ServiceOfferingId, cancellationToken);

        if (service is null)
        {
            return;
        }

        var customerId = ticket.CustomerId ?? await EnsureWalkInBookingCustomerAsync(
            ticket.GuestName,
            ticket.GuestPhone,
            ticket.GuestEmail,
            cancellationToken);

        ticket.CustomerId = customerId;
        var start = DateTimeOffset.UtcNow;
        var duration = Math.Max(15, service.DurationMinutes);
        var booking = new BookingRecord
        {
            Id = Guid.NewGuid(),
            TenantId = ticket.TenantId,
            ServiceOfferingId = service.Id,
            CustomerId = customerId,
            StaffMemberId = ticket.StaffMemberId,
            GuestCount = Math.Clamp(ticket.PartySize, 1, ErrorCodes.MaxBookingGuests),
            StartAt = start,
            EndAt = start.AddMinutes(duration),
            Status = BookingStatus.CheckedIn,
            CustomerNotes = string.IsNullOrWhiteSpace(ticket.Notes)
                ? "Walk-in queue"
                : $"Walk-in queue: {ticket.Notes}",
            CreatedAt = start,
            UpdatedAt = start,
            Lines =
            [
                new BookingLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = ticket.TenantId,
                    ServiceOfferingId = service.Id,
                    ServiceName = service.Name,
                    PriceAmount = service.PriceAmount,
                    DurationMinutes = service.DurationMinutes,
                    SortOrder = 0,
                    IsAddOn = false,
                },
            ],
        };

        // Fix line booking id after we know booking id
        foreach (var line in booking.Lines)
        {
            line.BookingId = booking.Id;
        }

        dbContext.Bookings.Add(booking);
        ticket.BookingId = booking.Id;
    }

    private async Task NotifyCalledAsync(QueueTicket ticket, CancellationToken cancellationToken)
    {
        var businessName = await dbContext.Tenants.AsNoTracking()
            .Where(x => x.Id == ticket.TenantId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "the salon";

        var recipient = string.IsNullOrWhiteSpace(ticket.GuestPhone)
            ? ticket.TenantId.ToString()
            : ticket.GuestPhone;

        await notificationJobs.ScheduleSendAsync(
            new NotificationMessage(
                "email",
                recipient,
                $"You're up at {businessName}",
                $"Hi {ticket.GuestName}, you're next at {businessName}. Please head to the front desk.",
                ticket.TenantId,
                $"queue-called:{ticket.Id:D}"),
            cancellationToken);
    }

    private async Task<Result<QueueTicketResponse>> CreateTicketAsync(
        Guid tenantId,
        string displayName,
        string phone,
        string? email,
        Guid? serviceOfferingId,
        Guid? staffMemberId,
        int partySize,
        string? notes,
        CancellationToken cancellationToken)
    {
        var name = displayName?.Trim() ?? string.Empty;
        var normalizedPhone = NormalizePhone(phone);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return Result.Failure<QueueTicketResponse>(
                Error.Validation("Name and phone are required to join the queue."));
        }

        if (!TryNormalizeEmail(email, out var normalizedEmail, out var emailError))
        {
            return Result.Failure<QueueTicketResponse>(emailError!);
        }

        if (partySize < 1 || partySize > ErrorCodes.MaxBookingGuests)
        {
            return Result.Failure<QueueTicketResponse>(ErrorCodes.GuestLimitError());
        }

        if (serviceOfferingId is Guid sid)
        {
            var serviceOk = await dbContext.ServiceOfferings.AsNoTracking()
                .AnyAsync(x => x.Id == sid && x.TenantId == tenantId && x.IsActive, cancellationToken);
            if (!serviceOk)
            {
                return Result.Failure<QueueTicketResponse>(Error.NotFound("Service"));
            }
        }

        if (staffMemberId is Guid staffId)
        {
            var staffOk = await dbContext.StaffMembers.AsNoTracking()
                .AnyAsync(x => x.Id == staffId && x.TenantId == tenantId && x.IsActive, cancellationToken);
            if (!staffOk)
            {
                return Result.Failure<QueueTicketResponse>(Error.NotFound("Staff"));
            }
        }

        var policyCheck = await ValidateWalkInJoinAsync(
            tenantId,
            serviceOfferingId,
            staffMemberId,
            cancellationToken);
        if (policyCheck.IsFailure)
        {
            return Result.Failure<QueueTicketResponse>(policyCheck.Error);
        }

        var customerUpsert = await tenantCustomers.UpsertFromWalkInAsync(
            tenantId,
            name,
            normalizedPhone,
            normalizedEmail,
            cancellationToken);
        if (customerUpsert.IsFailure)
        {
            return Result.Failure<QueueTicketResponse>(customerUpsert.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var ticket = new QueueTicket
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceOfferingId = serviceOfferingId,
            StaffMemberId = staffMemberId,
            CustomerId = customerUpsert.Value!.LinkedPlatformCustomerId,
            TenantCustomerId = customerUpsert.Value.TenantCustomerId,
            GuestName = name,
            GuestPhone = normalizedPhone,
            GuestEmail = normalizedEmail,
            Status = QueueTicketStatus.Waiting,
            PartySize = partySize,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            PublicToken = CreatePublicToken(),
            Source = QueueTicketSource.WalkIn,
            JoinedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.QueueTickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);
        await TenantRealtimeNotifications.PublishQueueFloorAsync(tenantId, tenantRealtime, cancellationToken);

        ticket = await dbContext.QueueTickets
            .AsNoTracking()
            .Include(x => x.ServiceOffering)
            .Include(x => x.StaffMember)
            .FirstAsync(x => x.Id == ticket.Id, cancellationToken);

        return Result.Success(await MapAsync(ticket, cancellationToken));
    }

    private async Task<Result> ValidateWalkInJoinAsync(
        Guid tenantId,
        Guid? serviceOfferingId,
        Guid? staffMemberId,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.BusinessProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure(Error.NotFound("Business"));
        }

        var now = DateTimeOffset.UtcNow;
        if (!WalkInQueuePolicy.IsAcceptingWalkIns(profile, now))
        {
            return Result.Failure(ErrorCodes.QueueClosedError());
        }

        if (profile.MaxWalkInWaiting is int max && max > 0)
        {
            var waiting = await dbContext.QueueTickets.AsNoTracking()
                .CountAsync(
                    x => x.TenantId == tenantId && x.Status == QueueTicketStatus.Waiting,
                    cancellationToken);
            if (waiting >= max)
            {
                return Result.Failure(ErrorCodes.QueueFullError());
            }
        }

        if (profile.WalkInCalendarPolicy == WalkInCalendarPolicy.Strict)
        {
            var duration = await ResolveServiceDurationMinutesAsync(serviceOfferingId, cancellationToken);
            var waitingAhead = await dbContext.QueueTickets.AsNoTracking()
                .CountAsync(
                    x => x.TenantId == tenantId && x.Status == QueueTicketStatus.Waiting,
                    cancellationToken);
            var etaMinutes = Math.Max(duration, 15) * Math.Max(1, waitingAhead + 1);
            var projectedStart = now.AddMinutes(etaMinutes);
            var projectedEnd = projectedStart.AddMinutes(duration);

            if (await HasCalendarConflictAsync(
                    tenantId,
                    staffMemberId,
                    projectedStart,
                    projectedEnd,
                    excludeBookingId: null,
                    cancellationToken))
            {
                return Result.Failure(ErrorCodes.QueueNoCapacityError());
            }
        }

        return Result.Success();
    }

    private async Task<Result> ValidateStartAgainstCalendarAsync(
        QueueTicket ticket,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.BusinessProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == ticket.TenantId, cancellationToken);
        if (profile?.WalkInCalendarPolicy != WalkInCalendarPolicy.Strict)
        {
            return Result.Success();
        }

        var duration = await ResolveServiceDurationMinutesAsync(ticket.ServiceOfferingId, cancellationToken);
        var start = DateTimeOffset.UtcNow;
        var end = start.AddMinutes(duration);
        if (await HasCalendarConflictAsync(
                ticket.TenantId,
                ticket.StaffMemberId,
                start,
                end,
                ticket.BookingId,
                cancellationToken))
        {
            return Result.Failure(ErrorCodes.QueueNoCapacityError());
        }

        return Result.Success();
    }

    private async Task<bool> HasCalendarConflictAsync(
        Guid tenantId,
        Guid? staffMemberId,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? excludeBookingId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Bookings.AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.Status == BookingStatus.Confirmed
                && x.StartAt < end
                && x.EndAt > start);

        if (excludeBookingId is Guid bid)
        {
            query = query.Where(x => x.Id != bid);
        }

        if (staffMemberId is Guid staffId)
        {
            query = query.Where(x => x.StaffMemberId == null || x.StaffMemberId == staffId);
        }

        return await query.AnyAsync(cancellationToken);
    }

    private async Task<int> ResolveServiceDurationMinutesAsync(
        Guid? serviceOfferingId,
        CancellationToken cancellationToken)
    {
        if (serviceOfferingId is not Guid sid)
        {
            return 30;
        }

        var duration = await dbContext.ServiceOfferings.AsNoTracking()
            .Where(x => x.Id == sid)
            .Select(x => (int?)x.DurationMinutes)
            .FirstOrDefaultAsync(cancellationToken);

        return Math.Max(15, duration ?? 30);
    }

    private async Task<IReadOnlyList<QueueExpectedArrivalResponse>> LoadExpectedArrivalsAsync(
        Guid tenantId,
        Guid? requireStaffMemberId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var dayStart = now.Date;
        var dayEnd = dayStart.AddDays(1);

        var activeBookingIds = await dbContext.QueueTickets.AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.BookingId != null
                && x.Status != QueueTicketStatus.Cancelled
                && x.Status != QueueTicketStatus.NoShow
                && (x.Status != QueueTicketStatus.Done || x.CompletedAt >= dayStart))
            .Select(x => x.BookingId!.Value)
            .ToListAsync(cancellationToken);

        var bookingsQuery = dbContext.Bookings.AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.Status == BookingStatus.Confirmed
                && x.StartAt >= dayStart
                && x.StartAt < dayEnd
                && !activeBookingIds.Contains(x.Id));

        if (requireStaffMemberId is Guid staffId)
        {
            bookingsQuery = bookingsQuery.Where(x => x.StaffMemberId == null || x.StaffMemberId == staffId);
        }

        var bookings = await bookingsQuery
            .OrderBy(x => x.StartAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (bookings.Count == 0)
        {
            return Array.Empty<QueueExpectedArrivalResponse>();
        }

        var customerIds = bookings.Select(x => x.CustomerId).Distinct().ToArray();
        var customers = await dbContext.Customers.AsNoTracking()
            .Where(x => customerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var serviceIds = bookings.Select(x => x.ServiceOfferingId).Distinct().ToArray();
        var services = await dbContext.ServiceOfferings.AsNoTracking()
            .Where(x => serviceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var staffIds = bookings.Where(x => x.StaffMemberId.HasValue).Select(x => x.StaffMemberId!.Value).Distinct().ToArray();
        var staff = staffIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.StaffMembers.AsNoTracking()
                .Where(x => staffIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        return bookings.Select(b =>
        {
            customers.TryGetValue(b.CustomerId, out var customer);
            services.TryGetValue(b.ServiceOfferingId, out var serviceName);
            string? staffName = null;
            if (b.StaffMemberId is Guid sid && staff.TryGetValue(sid, out var name))
            {
                staffName = name;
            }

            return new QueueExpectedArrivalResponse(
                b.Id,
                b.StartAt,
                string.IsNullOrWhiteSpace(customer?.Name) ? "Guest" : customer!.Name,
                customer?.Phone,
                serviceName,
                staffName,
                b.StaffMemberId);
        }).ToList();
    }

    private static WalkInStatusResponse MapWalkInStatus(BusinessProfile profile, DateTimeOffset now) =>
        new(
            WalkInQueuePolicy.IsAcceptingWalkIns(profile, now),
            profile.WalkInsEnabled,
            profile.WalkInAcceptingUntil,
            profile.MaxWalkInWaiting,
            WalkInQueuePolicy.ToApiPolicy(profile.WalkInCalendarPolicy));

    private static WalkInSettingsResponse ToWalkInSettingsResponse(BusinessProfile profile, DateTimeOffset now) =>
        new(
            profile.WalkInsEnabled,
            profile.WalkInAcceptingUntil,
            profile.MaxWalkInWaiting,
            WalkInQueuePolicy.ToApiPolicy(profile.WalkInCalendarPolicy),
            WalkInQueuePolicy.IsAcceptingWalkIns(profile, now));

    /// <summary>
    /// Synthetic platform customer for walk-in queue → booking FK when no Auth0 customer is linked.
    /// </summary>
    private async Task<Guid> EnsureWalkInBookingCustomerAsync(
        string name,
        string phone,
        string? email,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Customers
            .FirstOrDefaultAsync(x => x.Phone == phone, cancellationToken);

        if (existing is not null)
        {
            var touched = false;
            if (string.IsNullOrWhiteSpace(existing.Name))
            {
                existing.Name = name;
                touched = true;
            }

            if (!string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(existing.Email))
            {
                existing.Email = email;
                touched = true;
            }

            if (touched)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return existing.Id;
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Auth0Sub = $"queue|{phone}",
            Name = name,
            Phone = phone,
            Email = email,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
        return customer.Id;
    }

    private async Task<Result<bool>> EnsureQueueCapabilityAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.BusinessProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<bool>(Error.NotFound("Business"));
        }

        if (!capabilities.HasCapability(profile.BusinessType, profile.CategorySlug, BusinessCapability.WalkInQueue))
        {
            return Result.Failure<bool>(Error.Forbidden("Walk-in queue is not enabled for this business."));
        }

        return Result.Success(true);
    }

    private async Task<Guid?> ResolveTenantIdBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var normalized = slug?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return await (
            from loc in dbContext.BusinessLocations.AsNoTracking()
            join tenant in dbContext.Tenants.AsNoTracking() on loc.TenantId equals tenant.Id
            where loc.IsActive && loc.Slug == normalized && tenant.Status == TenantStatus.Verified
            select (Guid?)loc.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<QueueTicketResponse> MapAsync(QueueTicket ticket, CancellationToken cancellationToken)
    {
        var waitingAhead = 0;
        int? eta = null;
        if (ticket.Status is QueueTicketStatus.Waiting or QueueTicketStatus.Called)
        {
            var ahead = await dbContext.QueueTickets.AsNoTracking()
                .Where(x =>
                    x.TenantId == ticket.TenantId
                    && x.Status == QueueTicketStatus.Waiting
                    && x.JoinedAt < ticket.JoinedAt)
                .Select(x => new { x.ServiceOfferingId })
                .ToListAsync(cancellationToken);

            waitingAhead = ahead.Count;
            var serviceIds = ahead
                .Where(x => x.ServiceOfferingId.HasValue)
                .Select(x => x.ServiceOfferingId!.Value)
                .Distinct()
                .ToArray();

            var durations = serviceIds.Length == 0
                ? new Dictionary<Guid, int>()
                : await dbContext.ServiceOfferings.AsNoTracking()
                    .Where(x => serviceIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, x => x.DurationMinutes, cancellationToken);

            var minutes = ahead.Sum(x =>
                x.ServiceOfferingId is Guid sid && durations.TryGetValue(sid, out var d)
                    ? Math.Max(15, d)
                    : 30);
            // include self service duration roughly for "your wait"
            eta = minutes;
        }

        return new QueueTicketResponse(
            ticket.Id,
            ticket.TenantId,
            ToStatus(ticket.Status),
            waitingAhead + 1,
            eta,
            ticket.GuestName,
            ticket.GuestPhone,
            ticket.GuestEmail,
            ticket.ServiceOffering?.Name,
            ticket.ServiceOfferingId,
            ticket.StaffMember?.DisplayName,
            ticket.StaffMemberId,
            ticket.PartySize,
            ticket.Notes,
            ticket.JoinedAt,
            ticket.PublicToken,
            ticket.BookingId,
            Source: WalkInQueuePolicy.ToApiSource(ticket.Source),
            TenantCustomerId: ticket.TenantCustomerId);
    }

    private static string ToStatus(QueueTicketStatus status) => status switch
    {
        QueueTicketStatus.Waiting => "waiting",
        QueueTicketStatus.Called => "called",
        QueueTicketStatus.InService => "in_service",
        QueueTicketStatus.Done => "done",
        QueueTicketStatus.NoShow => "no_show",
        QueueTicketStatus.Cancelled => "cancelled",
        _ => "waiting",
    };

    private static bool TryNormalizeEmail(
        string? email,
        out string? normalized,
        out Error? error)
    {
        normalized = null;
        error = null;
        if (string.IsNullOrWhiteSpace(email))
        {
            return true;
        }

        var trimmed = email.Trim();
        if (trimmed.Length > 320)
        {
            error = Error.Validation("Enter a valid email address.");
            return false;
        }

        try
        {
            var parsed = new System.Net.Mail.MailAddress(trimmed);
            if (!string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                error = Error.Validation("Enter a valid email address.");
                return false;
            }

            normalized = parsed.Address.ToLowerInvariant();
            return true;
        }
        catch (FormatException)
        {
            error = Error.Validation("Enter a valid email address.");
            return false;
        }
    }

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var cleaned = PhoneDigits.Replace(phone.Trim(), string.Empty);
        return cleaned.Length >= 7 ? cleaned : string.Empty;
    }

    private static string CreatePublicToken()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
