namespace Adeni.Infrastructure.Tests.Tenancy;

using Adeni.Infrastructure.Tests.SqlServer;
using System.Data.Common;
using Adeni.Application.Booking;
using Adeni.Application.Tenancy;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

[Trait("Category", "SqlServer")]
[Collection(SqlServerTestCollection.Name)]
public sealed class CustomerSegmentsSqlServerTests
{
    private sealed class SqlFactAttribute : FactAttribute
    { public SqlFactAttribute() { if (Environment.GetEnvironmentVariable("ADENI_SQLSERVER_TESTS") != "1") Skip = "Requires isolated SQL Server published from DACPAC."; } }
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-26T12:00:00Z");
    private static CustomerSegmentWindow Window => new(Now,
        new DateTimeOffset(Now.UtcDateTime.Date.AddDays(-29), TimeSpan.Zero),
        new DateTimeOffset(Now.UtcDateTime.Date.AddDays(-89), TimeSpan.Zero),
        new DateTimeOffset(Now.UtcDateTime.Date.AddDays(-179), TimeSpan.Zero));
    private sealed class Counter : DbCommandInterceptor
    {
        public int Reads;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Reads++; return ValueTask.FromResult(result); }
    }
    private static AdeniDbContext Db(Counter? counter = null)
    {
        var builder = new DbContextOptionsBuilder<AdeniDbContext>().UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__AdeniDb"));
        if (counter is not null) builder.AddInterceptors(counter);
        return new(builder.Options, new TenantContext());
    }
    private static async Task<(Guid Tenant, Guid Location, Guid Service, CustomerProfileScope Scope)> Seed(AdeniDbContext db)
    {
        var tenant = Guid.NewGuid(); var location = Guid.NewGuid(); var customer = Guid.NewGuid(); var roster = Guid.NewGuid(); var service = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenant, Name = "SQL segments", CreatedAt = Now });
        db.BusinessProfiles.Add(new BusinessProfile { TenantId = tenant, CategorySlug = "barbers", Phone = "", UpdatedAt = Now });
        db.BusinessLocations.Add(new BusinessLocation { Id = location, TenantId = tenant, Slug = location.ToString(), Name = "Main", MarketId = "lagos", TimeZoneId = "Africa/Lagos", CreatedAt = Now, UpdatedAt = Now });
        db.Customers.Add(new Customer { Id = customer, Name = "SQL segments", Auth0Sub = customer.ToString(), CreatedAt = Now });
        db.TenantCustomers.Add(new TenantCustomer { Id = roster, TenantId = tenant, CustomerId = customer, DisplayName = "SQL segments", CreatedAt = Now, UpdatedAt = Now, LastSeenAt = Now });
        db.ServiceOfferings.Add(new ServiceOffering { Id = service, TenantId = tenant, Name = "Care", Currency = "CAD", CreatedAt = Now, UpdatedAt = Now });
        await db.SaveChangesAsync(); return (tenant, location, service, new(tenant, roster, customer, location));
    }
    private static BookingRecord Booking(Guid tenant, Guid location, Guid service, Guid customer, int days) => new()
    { Id = Guid.NewGuid(), TenantId = tenant, BusinessLocationId = location, ServiceOfferingId = service, CustomerId = customer, Status = BookingStatus.Completed,
        StartAt = Now.AddDays(-days), EndAt = Now.AddDays(-days).AddHours(1), CreatedAt = Now, UpdatedAt = Now };
    private static QueueTicket Queue(CustomerProfileScope scope, Guid? booking = null, QueueTicketStatus status = QueueTicketStatus.Done, DateTimeOffset? noShow = null) => new()
    { Id = Guid.NewGuid(), TenantId = scope.TenantId, TenantCustomerId = scope.TenantCustomerId, BusinessLocationId = scope.LocationId, BookingId = booking, Status = status,
        PublicToken = Guid.NewGuid().ToString(), GuestName = "", GuestPhone = "", JoinedAt = Now.AddDays(-1), CompletedAt = status == QueueTicketStatus.Done ? Now.AddDays(-1) : null,
        NoShowAt = noShow, CreatedAt = Now, UpdatedAt = Now, PartySize = 4 };

    [SqlFact]
    public async Task Facts_DeduplicateLinkedVisits_ResolveWalkInsWithoutPlatformLink_AndKeepBranchesSeparate()
    {
        await using var db = Db(); await using var tx = await db.Database.BeginTransactionAsync(); var s = await Seed(db);
        var booking = Booking(s.Tenant, s.Location, s.Service, s.Scope.CustomerId!.Value, 1);
        var walkIn = s.Scope with { TenantCustomerId = Guid.NewGuid(), CustomerId = null };
        db.TenantCustomers.Add(new TenantCustomer { Id = walkIn.TenantCustomerId, TenantId = s.Tenant, DisplayName = "Walk-in", CreatedAt = Now, UpdatedAt = Now, LastSeenAt = Now });
        var synthetic = Booking(s.Tenant, s.Location, s.Service, Guid.NewGuid(), 2);
        db.Bookings.AddRange(booking, synthetic);
        db.QueueTickets.AddRange(Queue(s.Scope, booking.Id), Queue(s.Scope, booking.Id), Queue(walkIn, synthetic.Id), Queue(walkIn, synthetic.Id), Queue(walkIn));
        var next = Booking(s.Tenant, s.Location, s.Service, s.Scope.CustomerId.Value, -1); next.Status = BookingStatus.Pending; db.Bookings.Add(next);
        await db.SaveChangesAsync();
        var rows = await new CustomerSegmentFactsQuery(db).GetAsync([s.Scope, walkIn], Window, default);
        var normal = rows.Single(x => x.TenantCustomerId == s.Scope.TenantCustomerId); Assert.Equal(1, normal.CompletedVisits); Assert.True(normal.HasUpcomingBooking);
        var walk = rows.Single(x => x.TenantCustomerId == walkIn.TenantCustomerId); Assert.Equal(2, walk.CompletedVisits); Assert.False(walk.Partial);
        Assert.Equal(0, Assert.Single(await new CustomerSegmentFactsQuery(db).GetAsync([s.Scope with { LocationId = Guid.NewGuid() }], Window, default)).CompletedVisits);
        Assert.Equal(0, Assert.Single(await new CustomerSegmentFactsQuery(db).GetAsync([s.Scope with { TenantId = Guid.NewGuid() }], Window, default)).CompletedVisits);
        await tx.RollbackAsync();
    }

    [SqlFact]
    public async Task Facts_UseExactWindowEdges_ExplicitNoShows_AndExposeIncompleteDates()
    {
        await using var db = Db(); await using var tx = await db.Database.BeginTransactionAsync(); var s = await Seed(db);
        foreach (var days in new[] { 29, 30, 89, 90, 179, 180 }) db.Bookings.Add(Booking(s.Tenant, s.Location, s.Service, s.Scope.CustomerId!.Value, days));
        db.QueueTickets.AddRange(Queue(s.Scope, status: QueueTicketStatus.NoShow, noShow: Window.NoShowFrom), Queue(s.Scope, status: QueueTicketStatus.NoShow, noShow: Window.NoShowFrom.AddTicks(-1)),
            Queue(s.Scope, status: QueueTicketStatus.Cancelled, noShow: Now.AddDays(-1)));
        await db.SaveChangesAsync();
        var row = Assert.Single(await new CustomerSegmentFactsQuery(db).GetAsync([s.Scope], Window, default));
        Assert.Equal(6, row.CompletedVisits); Assert.Equal(1, row.RecentVisits); Assert.Equal(5, row.FrequencyVisits); Assert.Equal(1, row.QueueNoShows);
        var legacy = Queue(s.Scope); legacy.CompletedAt = null; db.QueueTickets.Add(legacy); await db.SaveChangesAsync();
        Assert.True(Assert.Single(await new CustomerSegmentFactsQuery(db).GetAsync([s.Scope], Window, default)).ApproximateVisitDates);
        db.QueueTickets.Add(Queue(s.Scope, status: QueueTicketStatus.NoShow)); await db.SaveChangesAsync();
        var partial = Assert.Single(await new CustomerSegmentFactsQuery(db).GetAsync([s.Scope], Window, default)); Assert.True(partial.Partial);
        await tx.RollbackAsync();
    }

    [SqlFact]
    public async Task Facts_BatchUsesBoundedSetQueries_AndDanglingLinksArePartial()
    {
        var counter = new Counter(); await using var db = Db(counter); await using var tx = await db.Database.BeginTransactionAsync(); var s = await Seed(db);
        var scopes = Enumerable.Range(0, 40).Select(_ => s.Scope with { TenantCustomerId = Guid.NewGuid(), CustomerId = null }).ToArray();
        db.QueueTickets.Add(Queue(scopes[0], Guid.NewGuid())); await db.SaveChangesAsync(); counter.Reads = 0;
        var rows = await new CustomerSegmentFactsQuery(db).GetAsync(scopes, Window, default);
        Assert.Equal(40, rows.Count); Assert.InRange(counter.Reads, 1, 8); Assert.True(rows[0].Partial); Assert.All(rows.Skip(1), x => Assert.False(x.Partial));
        await tx.RollbackAsync();
    }
}
