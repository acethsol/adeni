namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Calendar;
using Adeni.Domain.Booking;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CalendarBookingQuery(AdeniDbContext db) : ICalendarBookingQuery
{
    public async Task<IReadOnlyList<CalendarBookingFact>> ReadAsync(Guid tenant, DateTimeOffset start, DateTimeOffset end,
        IReadOnlyList<Guid> mappedIds, CancellationToken ct) => await db.Bookings.AsNoTracking()
        .Where(x => x.TenantId == tenant && ((x.StartAt < end && x.EndAt > start) || mappedIds.Contains(x.Id)))
        .Select(x => new CalendarBookingFact(x.Id, x.TenantId, x.StaffMemberId, x.StartAt, x.EndAt,
            x.Status == BookingStatus.Pending || x.Status == BookingStatus.Confirmed,
            x.Status == BookingStatus.Confirmed || x.Status == BookingStatus.CheckedIn || x.Status == BookingStatus.Completed || x.Status == BookingStatus.Paid, x.ServiceOfferingId, x.BusinessLocationId, x.Status == BookingStatus.CheckedIn, x.Status == BookingStatus.Completed || x.Status == BookingStatus.Paid))
        .ToArrayAsync(ct);
}
