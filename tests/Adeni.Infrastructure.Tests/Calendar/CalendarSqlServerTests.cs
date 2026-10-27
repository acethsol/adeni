namespace Adeni.Infrastructure.Tests.Calendar;

using Adeni.Domain.Calendar;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Calendar;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static Adeni.Infrastructure.Tests.Calendar.CalendarTestSupport;

[Trait("Category", "SqlServer")]
public sealed class CalendarSqlServerTests
{
    private sealed class SqlFactAttribute : FactAttribute
    { public SqlFactAttribute() { if (Environment.GetEnvironmentVariable("ADENI_SQLSERVER_TESTS") != "1") Skip = "Requires isolated SQL Server published from DACPAC."; } }
    private static AdeniDbContext Db(IInterceptor? interceptor = null)
    {
        var o = new DbContextOptionsBuilder<AdeniDbContext>().UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__AdeniDb"));
        if (interceptor != null) o.AddInterceptors(interceptor);
        return new(o.Options, new TenantContext());
    }
    private static async Task Cleanup(Guid tenant)
    {
        await using var db = Db();
        await db.Set<ExternalBookingEvent>().Where(x => x.TenantId == tenant).ExecuteDeleteAsync();
        await db.Set<CalendarConflict>().Where(x => x.TenantId == tenant).ExecuteDeleteAsync();
        await db.Set<ExternalBusyBlock>().Where(x => x.TenantId == tenant).ExecuteDeleteAsync();
        await db.Set<CalendarSnapshot>().Where(x => x.TenantId == tenant).ExecuteDeleteAsync();
        await db.Set<CalendarSelection>().Where(x => x.TenantId == tenant).ExecuteDeleteAsync();
        await db.Set<CalendarOAuthAttempt>().Where(x => x.TenantId == tenant).ExecuteDeleteAsync();
        await db.Set<CalendarConnection>().Where(x => x.TenantId == tenant).ExecuteDeleteAsync();
        await db.Set<CalendarStaffState>().Where(x => x.TenantId == tenant).ExecuteDeleteAsync();
        await db.Bookings.Where(x => x.TenantId == tenant).ExecuteDeleteAsync();
        await db.ServiceOfferings.Where(x => x.TenantId == tenant).ExecuteDeleteAsync();
        await db.StaffMembers.Where(x => x.TenantId == tenant).ExecuteDeleteAsync();
        await db.BusinessUsers.Where(x => x.TenantId == tenant).ExecuteDeleteAsync();
        await db.Tenants.Where(x => x.Id == tenant).ExecuteDeleteAsync();
    }
    private static async Task<Guid> ServiceId(AdeniDbContext db, Guid tenant)
    {
        var service = new ServiceOffering { Id=Guid.NewGuid(),TenantId=tenant,Name="Service",PriceAmount=10,Currency="CAD",DurationMinutes=30 };
        db.Add(service);await db.SaveChangesAsync();return service.Id;
    }
    [SqlFact]
    public async Task SnapshotPublication_RacesAdmission_AndRollsBackBookingInsert()
    {
        await using var seed=Db();var s=await SeedCalendar(seed);
        try {
            var service=await ServiceId(seed,s.Tenant);var p=new FakeProvider();await Job(seed,p).SynchronizeAsync(s.Tenant,s.Connection.Id,default);
            await using var admission=Db();var availability=new ExternalCalendarAvailability(admission,new Clock());
            var result=Assert.Single(await availability.ReadAsync(s.Tenant,[s.Staff],Now.AddHours(1),Now.AddHours(2),true,default));Assert.True(result.HasCoverage);
            var id=Guid.NewGuid();admission.Add(new BookingRecord{Id=id,TenantId=s.Tenant,ServiceOfferingId=service,CustomerId=Guid.NewGuid(),StaffMemberId=s.Staff,StartAt=Now.AddHours(1),EndAt=Now.AddHours(2)});
            await using(var publication=Db()) { var guard=await publication.Set<CalendarStaffState>().SingleAsync(x=>x.Id==s.Staff);guard.AdmissionStamp=Guid.NewGuid();await publication.SaveChangesAsync(); }
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>admission.SaveChangesAsync());
            await using var check=Db();Assert.False(await check.Bookings.AnyAsync(x=>x.Id==id));
        } finally {await Cleanup(s.Tenant);}
    }
    [SqlFact]
    public async Task TenantKeys_RejectCrossTenantSelectionsAndDuplicateConnections()
    {
        await using var seed=Db();var s=await SeedCalendar(seed);
        try {
            await using(var invalid=Db()) {invalid.Add(new CalendarSelection{Id=Guid.NewGuid(),TenantId=Guid.NewGuid(),ConnectionId=s.Connection.Id,ExternalId="bad",ExternalIdHash="bad",TimeZoneId="UTC"});await Assert.ThrowsAsync<DbUpdateException>(()=>invalid.SaveChangesAsync());}
            await using(var duplicate=Db()) {duplicate.Add(new CalendarConnection{Id=Guid.NewGuid(),TenantId=s.Tenant,StaffMemberId=s.Staff,Provider="google",Generation=Guid.NewGuid()});await Assert.ThrowsAsync<DbUpdateException>(()=>duplicate.SaveChangesAsync());}
        } finally {await Cleanup(s.Tenant);}
    }
    [SqlFact]
    public async Task Mapping_UniquePerBookingConnection_AndTenantFkEnforced()
    {
        await using var seed=Db();var s=await SeedCalendar(seed);
        try {
            var service=await ServiceId(seed,s.Tenant);var booking=Guid.NewGuid();seed.Add(new BookingRecord{Id=booking,TenantId=s.Tenant,ServiceOfferingId=service,CustomerId=Guid.NewGuid(),StartAt=Now,EndAt=Now.AddHours(1)});await seed.SaveChangesAsync();
            seed.Add(new ExternalBookingEvent{Id=Guid.NewGuid(),TenantId=s.Tenant,ConnectionId=s.Connection.Id,BookingId=booking,TargetId="output"});await seed.SaveChangesAsync();
            await using(var duplicate=Db()){duplicate.Add(new ExternalBookingEvent{Id=Guid.NewGuid(),TenantId=s.Tenant,ConnectionId=s.Connection.Id,BookingId=booking,TargetId="output"});await Assert.ThrowsAsync<DbUpdateException>(()=>duplicate.SaveChangesAsync());}
            await using(var cross=Db()){cross.Add(new ExternalBookingEvent{Id=Guid.NewGuid(),TenantId=Guid.NewGuid(),ConnectionId=s.Connection.Id,BookingId=booking,TargetId="output"});await Assert.ThrowsAsync<DbUpdateException>(()=>cross.SaveChangesAsync());}
        } finally {await Cleanup(s.Tenant);}
    }
    [SqlFact]
    public async Task Disconnect_FencesLateRefreshOrWorkerSave_WithoutCredentialResurrection()
    {
        await using var seed=Db();var s=await SeedCalendar(seed);
        try {
            var p=new FakeProvider();await using var late=Db();
            var session=new CalendarConnectionSession(late,[p],new(Options()),Options(),new Clock());var opened=await session.OpenAsync(s.Tenant,s.Connection.Id,default);
            await using(var disconnect=Db())Assert.True((await Service(disconnect,p).DisconnectAsync(s.Tenant,"owner-"+s.Tenant,s.Connection.Id,default)).IsSuccess);
            opened.Connection.ProtectedCredentials="late-secret";
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>session.ReleaseAsync(opened.Connection,default));
            await using var check=Db();var c=await check.Set<CalendarConnection>().SingleAsync(x=>x.Id==s.Connection.Id);Assert.Equal("disconnected",c.Status);Assert.Equal("",c.ProtectedCredentials);
        } finally {await Cleanup(s.Tenant);}
    }
    [SqlFact]
    public async Task DisconnectDuringProviderRead_PreventsLateSnapshotPublication()
    {
        await using var seed=Db(); var s=await SeedCalendar(seed);
        try {
            var provider=new FakeProvider(); provider.Blocks.Add(new(Now.AddHours(1),Now.AddHours(2)));
            provider.BeforeBusy=async()=> { provider.BeforeBusy=null; await using var disconnect=Db(); Assert.True((await Service(disconnect,provider).DisconnectAsync(s.Tenant,"owner-"+s.Tenant,s.Connection.Id,default)).IsSuccess); };
            await Job(seed,provider).SynchronizeAsync(s.Tenant,s.Connection.Id,default);
            await using var check=Db(); Assert.Empty(await check.Set<CalendarSnapshot>().Where(x=>x.TenantId==s.Tenant).ToArrayAsync());
            Assert.Empty(await check.Set<ExternalBusyBlock>().Where(x=>x.TenantId==s.Tenant).ToArrayAsync());
            Assert.Equal("disconnected",(await check.Set<CalendarConnection>().SingleAsync(x=>x.Id==s.Connection.Id)).Status);
        } finally {await Cleanup(s.Tenant);}
    }
    [SqlFact]
    public async Task PartialReplacementConstraintFailure_RetainsOldSnapshotAndBusyBlocks()
    {
        await using var seed=Db();var s=await SeedCalendar(seed);
        try {
            var p=new FakeProvider();p.Blocks.Add(new(Now.AddHours(1),Now.AddHours(2)));await Job(seed,p).SynchronizeAsync(s.Tenant,s.Connection.Id,default);
            await using var bad=Db();var old=await bad.Set<ExternalBusyBlock>().SingleAsync(x=>x.TenantId==s.Tenant);bad.Remove(old);
            bad.Add(new ExternalBusyBlock{Id=Guid.NewGuid(),TenantId=s.Tenant,SnapshotId=old.SnapshotId,StartAt=Now.AddHours(2),EndAt=Now.AddHours(1)});
            var snapshot=await bad.Set<CalendarSnapshot>().SingleAsync(x=>x.Id==old.SnapshotId);snapshot.SyncedAt=Now.AddHours(3);
            await Assert.ThrowsAsync<DbUpdateException>(()=>bad.SaveChangesAsync());
            await using var check=Db();Assert.Equal(old.Id,(await check.Set<ExternalBusyBlock>().SingleAsync(x=>x.TenantId==s.Tenant)).Id);Assert.Equal(Now,(await check.Set<CalendarSnapshot>().SingleAsync(x=>x.Id==old.SnapshotId)).SyncedAt);
        } finally {await Cleanup(s.Tenant);}
    }
    [SqlFact]
    public async Task RepeatedSyncAndSelection_AreRelationallyIdempotent()
    {
        await using var seed=Db();var s=await SeedCalendar(seed);
        try {
            var p=new FakeProvider();p.Blocks.Add(new(Now.AddHours(1),Now.AddHours(2)));var service=Service(seed,p);
            for(var i=0;i<2;i++)Assert.True((await service.SelectAsync(s.Tenant,"owner-"+s.Tenant,s.Connection.Id,new([new("busy","America/Toronto",false)]),default)).IsSuccess);
            for(var i=0;i<2;i++)await Job(seed,p).SynchronizeAsync(s.Tenant,s.Connection.Id,default);
            await using var check=Db();Assert.Single(await check.Set<CalendarSnapshot>().Where(x=>x.TenantId==s.Tenant).ToArrayAsync());Assert.Single(await check.Set<ExternalBusyBlock>().Where(x=>x.TenantId==s.Tenant).ToArrayAsync());Assert.Single(await check.Set<CalendarSelection>().Where(x=>x.TenantId==s.Tenant).ToArrayAsync());
        } finally {await Cleanup(s.Tenant);}
    }
    [SqlFact]
    public async Task TwoWorkers_OnlyOneCanOwnConnectionLease()
    {
        await using var seed=Db();var s=await SeedCalendar(seed);
        try {
            var p=new FakeProvider();await using var a=Db();await using var b=Db();
            // Load the same SQL rowversion before either contender claims the lease.
            await a.Set<CalendarConnection>().SingleAsync(x=>x.Id==s.Connection.Id);await b.Set<CalendarConnection>().SingleAsync(x=>x.Id==s.Connection.Id);
            var first=new CalendarConnectionSession(a,[p],new(Options()),Options(),new Clock());var second=new CalendarConnectionSession(b,[p],new(Options()),Options(),new Clock());
            await first.OpenAsync(s.Tenant,s.Connection.Id,default);
            Assert.Equal("sync_in_progress",(await Assert.ThrowsAsync<CalendarProviderException>(()=>second.OpenAsync(s.Tenant,s.Connection.Id,default))).Category);
        } finally {await Cleanup(s.Tenant);}
    }
}
