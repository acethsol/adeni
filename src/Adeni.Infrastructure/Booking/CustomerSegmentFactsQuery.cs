namespace Adeni.Infrastructure.Booking;

using System.Linq.Expressions;
using Adeni.Application.Booking;
using Adeni.Application.Tenancy;
using Adeni.Domain.Booking;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerSegmentFactsQuery(AdeniDbContext db) : ICustomerSegmentFactsQuery
{
    private sealed class Visit
    {
        public Guid Key { get; init; }
        public Guid Id { get; init; }
        public DateTimeOffset At { get; init; }
        public bool Approximate { get; init; }
    }
    private sealed class Link
    {
        public QueueTicket Queue { get; init; } = null!;
        public BookingRecord? Booking { get; init; }
    }
    private sealed record Summary(Guid Key, int Count, int Recent, int Frequency, DateTimeOffset? Last, bool Approximate);

    private static IQueryable<Summary> Summarize(IQueryable<Visit> visits, CustomerSegmentWindow w) => visits.Distinct().GroupBy(x => x.Key)
        .Select(g => new Summary(g.Key, g.Count(), g.Count(x => x.At >= w.RecentFrom), g.Count(x => x.At >= w.FrequencyFrom),
            g.Max(x => (DateTimeOffset?)x.At), g.Any(x => x.Approximate)));

    public async Task<IReadOnlyList<CustomerSegmentFacts>> GetAsync(IReadOnlyList<CustomerProfileScope> scopes, CustomerSegmentWindow w, CancellationToken ct)
    {
        if (scopes.Count == 0) return [];
        if (scopes.Count > 100 || scopes.Any(x => x.TenantId != scopes[0].TenantId || x.LocationId != scopes[0].LocationId))
            throw new ArgumentException("Segment facts require one bounded tenant/location scope.", nameof(scopes));
        var tenant = scopes[0].TenantId;
        var location = scopes[0].LocationId;
        var ids = scopes.Select(x => x.TenantCustomerId).ToArray();
        var platformIds = scopes.Where(x => x.CustomerId != null).Select(x => x.CustomerId!.Value).ToArray();
        var bookings = db.Bookings.AsNoTracking().Where(x => x.TenantId == tenant && (location == null || x.BusinessLocationId == location));
        var queues = db.QueueTickets.AsNoTracking().Where(x => x.TenantId == tenant && x.TenantCustomerId != null
            && ids.Contains(x.TenantCustomerId.Value) && (location == null || x.BusinessLocationId == location));
        var complete = bookings.Where(x => x.StartAt <= w.AsOf && (x.Status == BookingStatus.Completed || x.Status == BookingStatus.Paid));
        var ordinary = await Summarize(complete.Where(x => platformIds.Contains(x.CustomerId))
            .Select(x => new Visit { Key = x.CustomerId, Id = x.Id, At = x.StartAt, Approximate = false }), w).ToArrayAsync(ct);
        var next = await bookings.Where(x => platformIds.Contains(x.CustomerId) && x.StartAt >= w.AsOf
            && (x.Status == BookingStatus.Pending || x.Status == BookingStatus.Confirmed))
            .Select(x => x.CustomerId).Distinct().ToArrayAsync(ct);

        // Only explicit links establish a walk-in's booking identity. Exclude links already counted via its platform identity.
        Expression<Func<Link, bool>> extra = x => false;
        foreach (var scope in scopes)
        {
            var id = scope.TenantCustomerId;
            var platform = scope.CustomerId;
            Expression<Func<Link, bool>> term = x => x.Queue.TenantCustomerId == id
                && (platform == null || x.Booking!.CustomerId != platform);
            var body = new ReplaceParameter(term.Parameters[0], extra.Parameters[0]).Visit(term.Body)!;
            extra = Expression.Lambda<Func<Link, bool>>(Expression.OrElse(extra.Body, body), extra.Parameters[0]);
        }
        var links = from queue in queues
            join booking in db.Bookings.AsNoTracking() on queue.BookingId equals booking.Id into matches
            from booking in matches.DefaultIfEmpty()
            select new Link { Queue = queue, Booking = booking };
        var linked = links.Where(x => x.Booking != null && x.Booking.TenantId == tenant
            && x.Booking.BusinessLocationId == x.Queue.BusinessLocationId).Where(extra);
        var linkedVisits = linked.Where(x => x.Booking!.StartAt <= w.AsOf
            && (x.Booking.Status == BookingStatus.Completed || x.Booking.Status == BookingStatus.Paid))
            .Select(x => new Visit { Key = x.Queue.TenantCustomerId!.Value, Id = x.Booking!.Id, At = x.Booking.StartAt, Approximate = false });
        var standalone = queues.Where(x => x.BookingId == null && x.Status == QueueTicketStatus.Done && (x.CompletedAt ?? x.JoinedAt) <= w.AsOf)
            .Select(x => new Visit { Key = x.TenantCustomerId!.Value, Id = x.Id, At = x.CompletedAt ?? x.JoinedAt, Approximate = x.CompletedAt == null });
        var walkIns = await Summarize(linkedVisits.Concat(standalone), w).ToArrayAsync(ct);
        var linkedNext = await linked.Where(x => x.Booking!.StartAt >= w.AsOf
            && (x.Booking.Status == BookingStatus.Pending || x.Booking.Status == BookingStatus.Confirmed))
            .Select(x => x.Queue.TenantCustomerId!.Value).Distinct().ToArrayAsync(ct);
        var noShows = await queues.Where(x => x.Status == QueueTicketStatus.NoShow && x.NoShowAt >= w.NoShowFrom && x.NoShowAt <= w.AsOf)
            .GroupBy(x => x.TenantCustomerId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }).ToArrayAsync(ct);
        var partial = await links.Where(x => (x.Queue.BookingId != null && (x.Booking == null || x.Booking.TenantId != tenant
                || x.Booking.BusinessLocationId != x.Queue.BusinessLocationId))
            || (x.Queue.Status == QueueTicketStatus.NoShow && x.Queue.NoShowAt == null))
            .Select(x => x.Queue.TenantCustomerId!.Value).Distinct().ToArrayAsync(ct);
        return scopes.Select(scope =>
        {
            var b = ordinary.SingleOrDefault(x => x.Key == scope.CustomerId);
            var q = walkIns.SingleOrDefault(x => x.Key == scope.TenantCustomerId);
            var last = new[] { b?.Last, q?.Last }.Max();
            return new CustomerSegmentFacts(scope.TenantCustomerId, (b?.Count ?? 0) + (q?.Count ?? 0),
                (b?.Recent ?? 0) + (q?.Recent ?? 0), (b?.Frequency ?? 0) + (q?.Frequency ?? 0), last,
                noShows.SingleOrDefault(x => x.Id == scope.TenantCustomerId)?.Count ?? 0,
                (scope.CustomerId is Guid c && next.Contains(c)) || linkedNext.Contains(scope.TenantCustomerId),
                partial.Contains(scope.TenantCustomerId), q?.Approximate ?? false);
        }).ToArray();
    }

    private sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
