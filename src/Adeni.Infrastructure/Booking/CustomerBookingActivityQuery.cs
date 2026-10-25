namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Tenancy;
using Adeni.Domain.Booking;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerBookingActivityQuery(AdeniDbContext db) : ICustomerBookingActivityQuery
{
    private IQueryable<BookingRecord> Bookings(CustomerProfileScope scope) => db.Bookings.AsNoTracking()
        .Where(x => x.TenantId == scope.TenantId && scope.CustomerId != null && x.CustomerId == scope.CustomerId
            && (scope.LocationId == null || x.BusinessLocationId == scope.LocationId));

    private IQueryable<QueueTicket> Queue(CustomerProfileScope scope) => db.QueueTickets.AsNoTracking()
        .Where(x => x.TenantId == scope.TenantId && x.TenantCustomerId == scope.TenantCustomerId
            && (scope.LocationId == null || x.BusinessLocationId == scope.LocationId));

    public async Task<CustomerBookingProfile> GetProfileAsync(CustomerProfileScope scope, DateTimeOffset asOf, bool includeAnalytics, CancellationToken cancellationToken)
    {
        CustomerVisitSummary? visits = null;
        if (includeAnalytics)
        {
            var appointments = await Bookings(scope).CountAsync(x => x.StartAt <= asOf
                && (x.Status == BookingStatus.Completed || x.Status == BookingStatus.Paid), cancellationToken);
            var walkIns = await Queue(scope).CountAsync(x => x.BookingId == null && x.JoinedAt <= asOf
                && x.Status == QueueTicketStatus.Done, cancellationToken);
            visits = new CustomerVisitSummary(appointments, walkIns);
        }
        var next = await Bookings(scope)
            .Where(x => x.StartAt >= asOf && (x.Status == BookingStatus.Pending || x.Status == BookingStatus.Confirmed))
            .OrderBy(x => x.StartAt).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.StartAt, x.Status, ServiceName = x.Lines.OrderBy(l => l.SortOrder).Select(l => l.ServiceName).FirstOrDefault() ?? (x.ServiceOffering != null ? x.ServiceOffering.Name : "") })
            .FirstOrDefaultAsync(cancellationToken);
        return new CustomerBookingProfile(visits, next is null ? null
            : new CustomerNextAppointment(next.Id, next.StartAt, next.ServiceName ?? "", next.Status.ToString()));
    }

    public async Task<CustomerActivityBatch> GetActivityAsync(CustomerProfileScope scope, DateTimeOffset from, DateTimeOffset asOf, int bookingLimit, CancellationToken cancellationToken)
    {
        var bookings = await Bookings(scope).Where(x => x.StartAt >= from && x.StartAt <= asOf)
            .OrderByDescending(x => x.StartAt).ThenByDescending(x => x.Id).Take(bookingLimit + 1)
            .Select(x => new { x.Id, x.BusinessLocationId, x.CreatedAt, x.StartAt, x.Status,
                ServiceName = x.Lines.OrderBy(l => l.SortOrder).Select(l => l.ServiceName).FirstOrDefault() ?? (x.ServiceOffering != null ? x.ServiceOffering.Name : "") })
            .ToArrayAsync(cancellationToken);
        var queues = await Queue(scope).Where(x => x.JoinedAt >= from && x.JoinedAt <= asOf)
            .OrderByDescending(x => x.JoinedAt).ThenByDescending(x => x.Id).Take(6)
            .Select(x => new { x.Id, x.BusinessLocationId, x.JoinedAt, x.CompletedAt, x.NoShowAt, x.Status,
                ServiceName = x.ServiceOffering != null ? x.ServiceOffering.Name : null })
            .ToArrayAsync(cancellationToken);
        var items = new List<CustomerActivityItem>();
        foreach (var b in bookings.Take(bookingLimit))
        {
            if (b.CreatedAt >= from && b.CreatedAt <= asOf)
                items.Add(new($"booking.created:{b.Id:N}", "bookingCreated", b.CreatedAt, "booking", b.Id, b.BusinessLocationId, b.ServiceName, b.Status.ToString()));
            items.Add(new($"booking.scheduled:{b.Id:N}", "appointmentScheduled", b.StartAt, "booking", b.Id, b.BusinessLocationId, b.ServiceName, b.Status.ToString()));
        }
        foreach (var q in queues.Take(5))
        {
            items.Add(new($"queue.joined:{q.Id:N}", "queueJoined", q.JoinedAt, "queue", q.Id, q.BusinessLocationId, q.ServiceName, q.Status.ToString()));
            if (q.CompletedAt is { } completed && completed >= from && completed <= asOf)
                items.Add(new($"queue.completed:{q.Id:N}", "queueCompleted", completed, "queue", q.Id, q.BusinessLocationId, q.ServiceName, q.Status.ToString()));
            if (q.NoShowAt is { } noShow && noShow >= from && noShow <= asOf)
                items.Add(new($"queue.noShow:{q.Id:N}", "queueNoShow", noShow, "queue", q.Id, q.BusinessLocationId, q.ServiceName, q.Status.ToString()));
        }
        return new(items, bookings.Length > bookingLimit || queues.Length > 5);
    }
}
