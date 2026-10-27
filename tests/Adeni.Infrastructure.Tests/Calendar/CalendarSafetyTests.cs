namespace Adeni.Infrastructure.Tests.Calendar;

using System.Text.Json;
using Adeni.Application.Calendar;
using Adeni.Application.Booking;
using Adeni.Domain.Calendar;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Calendar;
using Adeni.Infrastructure.Identity;
using Adeni.Infrastructure.Booking;
using Microsoft.EntityFrameworkCore;
using static Adeni.Infrastructure.Tests.Calendar.CalendarTestSupport;

public sealed class CalendarSafetyTests
{
    [Fact]
    public void CredentialEnvelope_BindsTenantConnectionProvider_AndRedactsRepresentations()
    {
        var p = new CalendarCredentialProtector(Options()); var tenant = Guid.NewGuid(); var id = Guid.NewGuid();
        var encrypted = p.Protect(tenant, id, "google", "sensitive-refresh");
        Assert.DoesNotContain("sensitive-refresh", encrypted); Assert.Equal("sensitive-refresh", p.Unprotect(tenant, id, "google", encrypted));
        Assert.Throws<CalendarProviderException>(() => p.Unprotect(Guid.NewGuid(), id, "google", encrypted));
        Assert.Throws<CalendarProviderException>(() => p.Unprotect(tenant, Guid.NewGuid(), "google", encrypted));
        Assert.Throws<CalendarProviderException>(() => p.Unprotect(tenant, id, "microsoft", encrypted));
        Assert.DoesNotContain("secret", new CalendarGrant { AccessToken = "secret", RefreshToken = "secret" }.ToString());
    }
    [Fact]
    public void Configuration_RequiresSecretsOnlyForEnabledProviders()
    {
        Assert.True(new CalendarOptions().IsValid());
        var o = Options().Value; Assert.True(o.IsValid()); o.EncryptionKeys.Clear(); Assert.False(o.IsValid());
        o.Google.Enabled = false; Assert.True(o.IsValid()); o.Google.Enabled = true; o.ApiBaseUrl = "https://evil.test/path"; Assert.False(o.IsValid());
    }
    [Theory]
    [InlineData("owner", true)][InlineData("manager", true)][InlineData("practitioner", false)][InlineData("unknown", false)][InlineData("accountant", false)][InlineData("ops", false)]
    public async Task Management_UsesStrictPersistedRoles(string role, bool allowed)
    {
        await using var db = Memory(); var s = await SeedCalendar(db);
        var user = await db.BusinessUsers.SingleAsync(x => x.TenantId == s.Tenant); user.Role = role; await db.SaveChangesAsync();
        Assert.Equal(allowed, await new CalendarAccessQuery(db).CanManageAsync(s.Tenant, user.Auth0Sub, s.Staff, default));
        Assert.False(await new CalendarAccessQuery(db).CanManageAsync(Guid.NewGuid(), user.Auth0Sub, s.Staff, default));
        Assert.False(await new CalendarAccessQuery(db).CanManageAsync(s.Tenant, user.Auth0Sub, Guid.NewGuid(), default));
    }
    [Fact]
    public async Task OAuth_RejectsWrongCorrelation_Replay_ExpiredAndCrossTenantStaff()
    {
        await using var db = Memory(); var s = await SeedCalendar(db); var p = new FakeProvider(); var service = Service(db,p);
        var invalid = await service.ConnectAsync(s.Tenant, "owner-" + s.Tenant, new(Guid.NewGuid(), "google"), default); Assert.True(invalid.IsFailure);
        var created = await service.ConnectAsync(s.Tenant, "owner-" + s.Tenant, new(s.Staff, "google"), default);
        var ticket = created.Value!.LaunchUrl.Split("ticket=")[1]; var launch = await service.LaunchAsync(ticket, default); Assert.True(launch.IsSuccess);
        Assert.True((await service.LaunchAsync(ticket, default)).IsFailure);
        Assert.True((await service.CallbackAsync("google", ticket, new string('x',43), "code", default)).IsFailure);
        var success = await service.CallbackAsync("google", ticket, launch.Value!.Correlation, "code", default);
        // Existing binding is to a different account; explicit disconnect is required.
        Assert.True(success.IsFailure); Assert.True((await service.CallbackAsync("google", ticket, launch.Value.Correlation, "code", default)).IsFailure);
        var attempt = await db.Set<CalendarOAuthAttempt>().SingleAsync(); Assert.NotNull(attempt.ConsumedAt);
        var second = await service.ConnectAsync(s.Tenant, "owner-" + s.Tenant, new(s.Staff,"google"), default);
        var secondTicket = second.Value!.LaunchUrl.Split("ticket=")[1];
        var expired = await db.Set<CalendarOAuthAttempt>().SingleAsync(x => x.StateHash == CalendarCredentialProtector.Hash(secondTicket)); expired.ExpiresAt = Now.AddSeconds(-1); await db.SaveChangesAsync();
        Assert.True((await service.LaunchAsync(secondTicket,default)).IsFailure);
    }
    [Fact]
    public async Task SameProviderAccount_MayBeIndependentlyAuthorizedInTwoTenants_WithoutSharingCredentials()
    {
        await using var db = Memory(); var first = await SeedCalendar(db); var second = await SeedCalendar(db); var provider = new FakeProvider(); var service = Service(db, provider);
        foreach (var s in new[] { first, second })
        {
            Assert.True((await service.DisconnectAsync(s.Tenant, "owner-" + s.Tenant, s.Connection.Id, default)).IsSuccess);
            var created = await service.ConnectAsync(s.Tenant, "owner-" + s.Tenant, new(s.Staff, "google"), default);
            var ticket = created.Value!.LaunchUrl.Split("ticket=")[1]; var launch = await service.LaunchAsync(ticket, default);
            Assert.True((await service.CallbackAsync("google", ticket, launch.Value!.Correlation, "code", default)).IsSuccess);
        }
        var connections = await db.Set<CalendarConnection>().ToArrayAsync(); Assert.Equal(2, connections.Length);
        Assert.Equal(connections[0].AccountHash, connections[1].AccountHash); Assert.NotEqual(connections[0].ProtectedCredentials, connections[1].ProtectedCredentials);
        Assert.True((await service.ListAsync(second.Tenant, "owner-" + first.Tenant, default)).IsFailure);
    }
    [Fact]
    public async Task Snapshot_FreshnessEndpointsTenantIsolationAndExpansion_AreSafe()
    {
        await using var db = Memory(); var s = await SeedCalendar(db); var start = Now.AddHours(1); var end = start.AddHours(1);
        var snap = new CalendarSnapshot { Id = Guid.NewGuid(), TenantId = s.Tenant, ConnectionId = s.Connection.Id, StartAt = Now, EndAt = Now.AddDays(90), SyncedAt = Now };
        db.Add(snap); db.Add(new ExternalBusyBlock { Id = Guid.NewGuid(), TenantId = s.Tenant, SnapshotId = snap.Id, StartAt = start, EndAt = end }); await db.SaveChangesAsync();
        var service = new ExternalCalendarAvailability(db, new Clock());
        var fresh = Assert.Single(await service.ReadAsync(s.Tenant,[s.Staff],start,end,true,default)); Assert.True(fresh.HasCoverage); Assert.False(fresh.IsFree(start,end));
        Assert.True(fresh.IsFree(end,end.AddMinutes(30))); Assert.True(fresh.IsFree(start.AddHours(-1),start));
        Assert.Empty(await service.ReadAsync(Guid.NewGuid(),[s.Staff],start,end,false,default));
        snap.SyncedAt = Now.AddMinutes(-16); await db.SaveChangesAsync();
        Assert.False(Assert.Single(await service.ReadAsync(s.Tenant,[s.Staff],start,end,false,default)).HasCoverage);
        var beyond = start.AddDays(120); Assert.False(Assert.Single(await service.ReadAsync(s.Tenant,[s.Staff],beyond,beyond.AddHours(1),false,default)).HasCoverage);
        Assert.Contains(await db.Set<CalendarSnapshot>().ToArrayAsync(), x => x.StartAt == beyond && x.SyncedAt == DateTimeOffset.MinValue);
    }
    [Fact]
    public async Task Sync_FailureRetainsPriorCompleteSnapshot_AndRevocationRequiresReconnect()
    {
        await using var db = Memory(); var s = await SeedCalendar(db); var p = new FakeProvider(); p.Blocks.Add(new(Now.AddHours(1),Now.AddHours(2)));
        await Job(db,p).SynchronizeAsync(s.Tenant,s.Connection.Id,default); Assert.Single(await db.Set<ExternalBusyBlock>().ToArrayAsync());
        p.Failure="reconnect_required"; await Job(db,p).SynchronizeAsync(s.Tenant,s.Connection.Id,default);
        Assert.Single(await db.Set<ExternalBusyBlock>().ToArrayAsync()); Assert.Equal("reconnect_required",(await db.Set<CalendarConnection>().SingleAsync()).Status);
    }
    [Fact]
    public async Task ProviderFailures_BackOffAndHonorRetryAfter_AndRefreshRevocationReleasesLease()
    {
        await using var db=Memory(); var s=await SeedCalendar(db); var provider=new FakeProvider { Failure="provider_failure" };
        await Job(db,provider).SynchronizeAsync(s.Tenant,s.Connection.Id,default);
        await Job(db,provider).SynchronizeAsync(s.Tenant,s.Connection.Id,default);
        var c=await db.Set<CalendarConnection>().SingleAsync(); Assert.Equal(2,c.FailureCount); Assert.True(c.NextSyncAt >= Now.AddMinutes(10));
        provider.RetryAfter=TimeSpan.FromHours(2);provider.Failure="rate_limited";await Job(db,provider).SynchronizeAsync(s.Tenant,s.Connection.Id,default);
        c=await db.Set<CalendarConnection>().SingleAsync();Assert.Equal(Now.AddHours(2),c.NextSyncAt);
        Assert.True((await Service(db,provider).SyncAsync(s.Tenant,"owner-"+s.Tenant,c.Id,default)).IsFailure);
        var grant=new CalendarGrant { AccessToken="sensitive-access",RefreshToken="sensitive-refresh",ExpiresAt=Now.AddMinutes(-1) };
        c.ProtectedCredentials=new CalendarCredentialProtector(Options()).Protect(s.Tenant,c.Id,"google",JsonSerializer.Serialize(grant));await db.SaveChangesAsync();
        provider.RefreshFailure="reconnect_required";await Job(db,provider).SynchronizeAsync(s.Tenant,c.Id,default);
        c=await db.Set<CalendarConnection>().SingleAsync();Assert.Equal("reconnect_required",c.Status);Assert.Null(c.LeaseId);Assert.Null(c.LeaseUntil);
    }
    [Fact]
    public async Task Disconnect_RemovesCredentialsAndBusy_AndDoesNotExposeSecretsInStatus()
    {
        await using var db = Memory(); var s = await SeedCalendar(db); var p = new FakeProvider(); await Job(db,p).SynchronizeAsync(s.Tenant,s.Connection.Id,default);
        var service=Service(db,p); var result=await service.ListAsync(s.Tenant,"owner-"+s.Tenant,default);
        var json=JsonSerializer.Serialize(result.Value); Assert.DoesNotContain("sensitive-",json); Assert.DoesNotContain("ProtectedCredentials",json);
        Assert.True((await service.DisconnectAsync(s.Tenant,"owner-"+s.Tenant,s.Connection.Id,default)).IsSuccess);
        Assert.Equal("",(await db.Set<CalendarConnection>().SingleAsync()).ProtectedCredentials);
        Assert.Empty(await db.Set<ExternalBusyBlock>().ToArrayAsync()); Assert.Empty(await db.Set<CalendarSnapshot>().ToArrayAsync());
        Assert.Empty(await new ExternalCalendarAvailability(db,new Clock()).ReadAsync(s.Tenant,[s.Staff],Now,Now.AddHours(1),false,default));
    }
    [Theory]
    [InlineData("America/Toronto",2027,3,14,2,30,false)][InlineData("America/Toronto",2027,11,7,1,30,true)]
    [InlineData("Africa/Lagos",2027,3,14,2,30,true)]
    public void TimeZones_RejectSpringGap_AndChooseStandardFold(string zone,int year,int month,int day,int hour,int minute,bool valid)
    {
        var tz=SchedulingTimeZone.FromId(zone); var date=new DateTime(year,month,day); var time=new TimeOnly(hour,minute);
        Assert.Equal(valid,tz.IsValidLocalTime(date,time));
        if (!valid) Assert.Throws<ArgumentException>(()=>tz.ToUtc(date,time));
        else { var utc=tz.ToUtc(date,time); Assert.Equal(TimeSpan.Zero,utc.Offset); if(month==11)Assert.Equal(6,utc.Hour); if(zone=="Africa/Lagos")Assert.Equal(1,utc.Hour); }
    }
    [Fact]
    public void LateNightSlotGeneration_DoesNotWrapTimeOnlyPastClosingTime()
    {
        var start = new DateTimeOffset(2027,1,4,23,0,0,TimeSpan.Zero);
        var slots = SlotGenerator.GenerateSlotStarts(SchedulingTimeZone.FromId("UTC"),
            [new WeeklyAvailabilityRule(start.DayOfWeek,new(23,0),new(23,59))],start,start.AddHours(1),30).Take(2).ToArray();
        Assert.Single(slots); Assert.Equal(start,slots[0]);
    }
    [Fact]
    public void InvalidConfiguredTimeZone_DoesNotGenerateUtcFallbackSlots()
    {
        var zone = SchedulingTimeZone.FromId("Unknown/Invalid");
        Assert.False(zone.IsConfiguredZoneValid);
        Assert.Empty(SlotGenerator.GenerateSlotStarts(zone, [new WeeklyAvailabilityRule(Now.DayOfWeek,new(0,0),new(23,0))],Now,Now.AddHours(2),30));
    }
    [Theory]
    [InlineData("2027-03-14", "2027-03-15", "Eastern Standard Time", 23)]
    [InlineData("2027-11-07", "2027-11-08", "America/Toronto", 25)]
    [InlineData("2027-03-14", "2027-03-15", "Africa/Lagos", 24)]
    public void GraphInstant_UsesExplicitZoneAndAllDayDstBoundaries(string from, string to, string zone, int hours)
    {
        using var start=JsonDocument.Parse(JsonSerializer.Serialize(new { dateTime = from + "T00:00:00", timeZone = zone }));
        using var end=JsonDocument.Parse(JsonSerializer.Serialize(new { dateTime = to + "T00:00:00", timeZone = zone }));
        Assert.Equal(hours,(MicrosoftCalendarProvider.ParseInstant(end.RootElement)-MicrosoftCalendarProvider.ParseInstant(start.RootElement)).TotalHours);
    }
}
