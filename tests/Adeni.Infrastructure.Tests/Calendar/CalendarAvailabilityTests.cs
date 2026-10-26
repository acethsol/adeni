namespace Adeni.Infrastructure.Tests.Calendar;

using Adeni.Application.Booking;
using Adeni.Domain.Booking;
using Adeni.Domain.Calendar;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using static Adeni.Infrastructure.Tests.Finance.CommerceTestSupport;
using static CalendarTestSupport;

public sealed class CalendarAvailabilityTests
{
    private sealed class Zone : ITenantSchedulingTimeZone
    {
        public Task<ISchedulingTimeZone> ForTenantAsync(Guid tenantId,CancellationToken ct=default)=>Task.FromResult<ISchedulingTimeZone>(SchedulingTimeZone.FromId("UTC"));
        public Task<ISchedulingTimeZone> ForLocationAsync(Guid locationId,CancellationToken ct=default)=>ForTenantAsync(locationId,ct);
    }
    [Fact]
    public async Task ListingAndAdmission_BlockBusyPractitioner_WithoutEmptyRosterFallback_AndKeepOtherStaffAvailable()
    {
        await using var db=Memory();var s=await SeedCalendar(db);var location=Guid.NewGuid();var service=Guid.NewGuid();
        var day=DateTimeOffset.UtcNow.AddDays(3).Date;var start=new DateTimeOffset(day.AddHours(10),TimeSpan.Zero);
        db.Add(new BusinessProfile{TenantId=s.Tenant});db.Add(new BusinessLocation{Id=location,TenantId=s.Tenant,Slug="calendar-booking",Name="Main",IsPrimary=true,TimeZoneId="UTC"});
        db.Add(new ServiceOffering{Id=service,TenantId=s.Tenant,Name="Service",DurationMinutes=30});
        db.Add(new WeeklyAvailability{Id=Guid.NewGuid(),TenantId=s.Tenant,BusinessLocationId=location,DayOfWeek=day.DayOfWeek,OpenTime=new(9,0),CloseTime=new(17,0)});
        var snapshot=new CalendarSnapshot{Id=Guid.NewGuid(),TenantId=s.Tenant,ConnectionId=s.Connection.Id,StartAt=start.AddDays(-1),EndAt=start.AddDays(1),SyncedAt=Now};db.Add(snapshot);
        db.Add(new ExternalBusyBlock{Id=Guid.NewGuid(),TenantId=s.Tenant,SnapshotId=snapshot.Id,StartAt=start.AddHours(-2),EndAt=start.AddHours(9)});await db.SaveChangesAsync();
        var availability=new AvailabilityService(db,new Zone(),new BusinessClosedDateService(db),new DistributedCacheService(new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()))),new Adeni.Infrastructure.Calendar.ExternalCalendarAvailability(db,new Clock()));
        var slots=await availability.GetAvailableSlotsBySlugAsync("calendar-booking",service,start,start.AddHours(2));Assert.True(slots.IsSuccess);Assert.Empty(slots.Value!);
        Assert.False(await availability.IsSlotAvailableAsync(s.Tenant,service,start,30,s.Staff,businessLocationId:location));
        Assert.False(await availability.IsSlotAvailableAsync(s.Tenant,service,start,30,businessLocationId:location));
        var other=Guid.NewGuid();db.Add(new StaffMember{Id=other,TenantId=s.Tenant,DisplayName="Available"});await db.SaveChangesAsync();
        Assert.NotEmpty((await availability.GetAvailableSlotsBySlugAsync("calendar-booking",service,start,start.AddHours(2))).Value!);
        Assert.True(await availability.IsSlotAvailableAsync(s.Tenant,service,start,30,businessLocationId:location));
        Assert.False(await availability.IsSlotAvailableAsync(s.Tenant,service,start,30,s.Staff,businessLocationId:location));
        var staff=await db.StaffMembers.SingleAsync(x=>x.Id==other);staff.BusinessLocationId=Guid.NewGuid();await db.SaveChangesAsync();
        Assert.False(await availability.IsSlotAvailableAsync(s.Tenant,service,start,30,other,businessLocationId:location));
        snapshot.SyncedAt=Now.AddMinutes(-16);await db.SaveChangesAsync();
        Assert.False(await availability.IsSlotAvailableAsync(s.Tenant,service,start,30,businessLocationId:location));
    }
    [Fact]
    public async Task CompletedHistoricalProjection_RetainsMappingAndCopy_WithoutConsumingActiveWorkBound()
    {
        await using var db = Memory(); var s = await SeedCalendar(db); var p = new FakeProvider(); var bookingId = Guid.NewGuid();
        db.Add(new CalendarSelection { Id=Guid.NewGuid(),TenantId=s.Tenant,ConnectionId=s.Connection.Id,ExternalId="output",ExternalIdHash="output",TimeZoneId="UTC",IsWriteTarget=true });
        db.Add(new BookingRecord { Id=bookingId,TenantId=s.Tenant,StaffMemberId=s.Staff,CustomerId=Guid.NewGuid(),ServiceOfferingId=Guid.NewGuid(),StartAt=Now.AddDays(-4),EndAt=Now.AddDays(-4).AddHours(1),Status=BookingStatus.Paid });
        db.Add(new ExternalBookingEvent { Id=Guid.NewGuid(),TenantId=s.Tenant,ConnectionId=s.Connection.Id,BookingId=bookingId,TargetId="output",ExternalId="owned-event",State="written" });
        await db.SaveChangesAsync(); await Job(db,p).SynchronizeAsync(s.Tenant,s.Connection.Id,default);
        Assert.Equal("archived",(await db.Set<ExternalBookingEvent>().SingleAsync()).State); Assert.Equal(0,p.Deletes); Assert.Equal(0,p.Writes);
        await Job(db,p).SynchronizeAsync(s.Tenant,s.Connection.Id,default); Assert.Single(await db.Set<ExternalBookingEvent>().ToArrayAsync());
    }
    [Fact]
    public async Task WriteBackAndConflict_DedupeMapping_KeepBooking_ThenDeleteCopyOnCancellation()
    {
        await using var db=Memory();var s=await SeedCalendar(db);var p=new FakeProvider();var id=Guid.NewGuid();
        db.Add(new CalendarSelection{Id=Guid.NewGuid(),TenantId=s.Tenant,ConnectionId=s.Connection.Id,ExternalId="output",ExternalIdHash="output",TimeZoneId="UTC",IsWriteTarget=true});
        var booking=new BookingRecord{Id=id,TenantId=s.Tenant,StaffMemberId=s.Staff,CustomerId=Guid.NewGuid(),ServiceOfferingId=Guid.NewGuid(),StartAt=Now.AddHours(1),EndAt=Now.AddHours(2),Status=BookingStatus.Confirmed};db.Add(booking);await db.SaveChangesAsync();
        p.Blocks.Add(new(booking.StartAt,booking.EndAt));
        for(var i=0;i<2;i++)await Job(db,p).SynchronizeAsync(s.Tenant,s.Connection.Id,default);
        Assert.Single(await db.Set<ExternalBookingEvent>().ToArrayAsync());Assert.Single(await db.Set<CalendarConflict>().ToArrayAsync());Assert.Equal(BookingStatus.Confirmed,booking.Status);
        var service=Service(db,p);var conflict=await db.Set<CalendarConflict>().SingleAsync();Assert.True((await service.AcknowledgeAsync(s.Tenant,"owner-"+s.Tenant,conflict.Id,default)).IsSuccess);
        Assert.Single(await db.Set<ExternalBusyBlock>().ToArrayAsync());
        booking.Status=BookingStatus.CheckedIn;await db.SaveChangesAsync();await Job(db,p).SynchronizeAsync(s.Tenant,s.Connection.Id,default);
        Assert.Null((await db.Set<CalendarConflict>().SingleAsync()).ResolvedAt);
        booking.Status=BookingStatus.Cancelled;await db.SaveChangesAsync();await Job(db,p).SynchronizeAsync(s.Tenant,s.Connection.Id,default);
        Assert.Equal(1,p.Deletes);Assert.Equal("deleted",(await db.Set<ExternalBookingEvent>().SingleAsync()).State);Assert.NotNull((await db.Set<CalendarConflict>().SingleAsync()).ResolvedAt);
        await Job(db,p).SynchronizeAsync(s.Tenant,s.Connection.Id,default);Assert.Equal(1,p.Deletes);
    }
}
