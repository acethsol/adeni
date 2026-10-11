namespace Adeni.Infrastructure.Booking;

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Adeni.Application.Booking;
using Adeni.Application.Catalog;
using Adeni.Application.Notifications;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class QueueService(
    AdeniDbContext dbContext,
    IBusinessCapabilitiesService capabilities,
    INotificationDispatcher notifications) : IQueueService
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
                ticket.StartedAt = now;
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

        var customerId = ticket.CustomerId ?? await EnsureGuestCustomerAsync(
            ticket.GuestName,
            ticket.GuestPhone,
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

        await notifications.SendAsync(
            new NotificationMessage(
                "email",
                recipient,
                $"You're up at {businessName}",
                $"Hi {ticket.GuestName}, you're next at {businessName}. Please head to the front desk.",
                ticket.TenantId),
            cancellationToken);
    }

    private async Task<Result<QueueTicketResponse>> CreateTicketAsync(
        Guid tenantId,
        string displayName,
        string phone,
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

        var customerId = await EnsureGuestCustomerAsync(name, normalizedPhone, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var ticket = new QueueTicket
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceOfferingId = serviceOfferingId,
            StaffMemberId = staffMemberId,
            CustomerId = customerId,
            GuestName = name,
            GuestPhone = normalizedPhone,
            Status = QueueTicketStatus.Waiting,
            PartySize = partySize,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            PublicToken = CreatePublicToken(),
            JoinedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.QueueTickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);

        ticket = await dbContext.QueueTickets
            .AsNoTracking()
            .Include(x => x.ServiceOffering)
            .Include(x => x.StaffMember)
            .FirstAsync(x => x.Id == ticket.Id, cancellationToken);

        return Result.Success(await MapAsync(ticket, cancellationToken));
    }

    private async Task<Guid> EnsureGuestCustomerAsync(
        string name,
        string phone,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Customers
            .FirstOrDefaultAsync(x => x.Phone == phone, cancellationToken);

        if (existing is not null)
        {
            if (string.IsNullOrWhiteSpace(existing.Name))
            {
                existing.Name = name;
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
            ticket.ServiceOffering?.Name,
            ticket.ServiceOfferingId,
            ticket.StaffMember?.DisplayName,
            ticket.StaffMemberId,
            ticket.PartySize,
            ticket.Notes,
            ticket.JoinedAt,
            ticket.PublicToken,
            ticket.BookingId);
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
