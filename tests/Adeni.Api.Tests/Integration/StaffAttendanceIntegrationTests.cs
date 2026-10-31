namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Adeni.Api.Middleware;
using Adeni.Application.Booking;
using Adeni.Application.Identity;
using Adeni.Application.Tenancy;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class StaffAttendanceIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public StaffAttendanceIntegrationTests(WebApplicationFactory<Program> factory) =>
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AdeniDb"] = "",
                ["Redis:ConnectionString"] = "",
                ["Auth0:Enabled"] = "false",
            }));
        });

    private sealed record AttendanceSeed(
        Guid TenantId,
        Guid LocationId,
        Guid StaffMemberId,
        Guid ManagerUserId,
        string ManagerSub,
        string PractitionerSub);

    private async Task<AttendanceSeed> SeedAsync(bool geofenceEnabled = true)
    {
        var tenantId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var managerUserId = Guid.NewGuid();
        var managerSub = $"auth0|mgr-{tenantId:N}";
        var practitionerSub = $"auth0|prac-{tenantId:N}";

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Attendance salon",
            Status = TenantStatus.Verified,
            SubscriptionSku = SubscriptionSku.Professional,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "hair-salons",
            Phone = "+2348000000001",
        });
        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = locationId,
            TenantId = tenantId,
            Slug = "main-" + tenantId.ToString("N")[..8],
            Name = "Main",
            AddressLine = "12 Admiralty Way",
            Area = "Lekki",
            MarketId = "lagos",
            IsPrimary = true,
            IsActive = true,
            Latitude = 6.5244,
            Longitude = 3.3792,
            AttendanceGeofenceEnabled = geofenceEnabled,
            AttendanceGeofenceRadiusMeters = 100,
        });
        db.StaffMembers.Add(new StaffMember
        {
            Id = staffId,
            TenantId = tenantId,
            DisplayName = "Ada",
            IsActive = true,
            BusinessLocationId = locationId,
        });
        db.BusinessUsers.Add(new BusinessUser
        {
            Id = managerUserId,
            TenantId = tenantId,
            Auth0Sub = managerSub,
            Role = "manager",
        });
        db.BusinessUsers.Add(new BusinessUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Auth0Sub = practitionerSub,
            Role = "practitioner",
            StaffMemberId = staffId,
        });
        await db.SaveChangesAsync();

        return new AttendanceSeed(tenantId, locationId, staffId, managerUserId, managerSub, practitionerSub);
    }

    private HttpClient Client(string sub, Guid tenantId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, sub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());
        return client;
    }

    [Fact]
    public async Task ClockIn_WithoutFix_FailsWhenGeofenceRequired()
    {
        var seed = await SeedAsync(geofenceEnabled: true);
        using var client = Client(seed.PractitionerSub, seed.TenantId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/tenant/attendance/me/events",
            new RecordStaffAttendanceEventRequest("clock_in", seed.LocationId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("staff.location_verification_failed", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ClockIn_WithValidFix_Succeeds()
    {
        var seed = await SeedAsync(geofenceEnabled: true);
        using var client = Client(seed.PractitionerSub, seed.TenantId);
        var now = DateTimeOffset.UtcNow;

        var response = await client.PostAsJsonAsync(
            "/api/v1/tenant/attendance/me/events",
            new RecordStaffAttendanceEventRequest(
                "clock_in",
                seed.LocationId,
                new StaffAttendanceLocationFixRequest(6.5244, 3.3792, 15, now)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ManagerOverride_ClocksInStaff_WhenGeofenceWouldFail()
    {
        var seed = await SeedAsync(geofenceEnabled: true);
        using var manager = Client(seed.ManagerSub, seed.TenantId);

        var response = await manager.PostAsJsonAsync(
            $"/api/v1/tenant/attendance/staff/{seed.StaffMemberId}/override",
            new ManagerStaffAttendanceOverrideRequest(
                "clock_in",
                seed.LocationId,
                "GPS unavailable indoors; verified at reception."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("override", body.GetProperty("verificationMethod").GetString());
    }

    [Fact]
    public async Task Practitioner_CannotGetTeamAttendance()
    {
        var seed = await SeedAsync();
        using var client = Client(seed.PractitionerSub, seed.TenantId);
        var response = await client.GetAsync("/api/v1/tenant/attendance/team");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ClockIn_CreatesActivePresenceSession_ClockOutClears()
    {
        var seed = await SeedAsync(geofenceEnabled: false);
        using var client = Client(seed.PractitionerSub, seed.TenantId);

        var clockIn = await client.PostAsJsonAsync(
            "/api/v1/tenant/attendance/me/events",
            new RecordStaffAttendanceEventRequest("clock_in", seed.LocationId));
        Assert.Equal(HttpStatusCode.Created, clockIn.StatusCode);

        var presence = await client.GetAsync("/api/v1/tenant/presence/me");
        Assert.Equal(HttpStatusCode.OK, presence.StatusCode);
        var session = await presence.Content.ReadFromJsonAsync<VerifiedPresenceSessionResponse>();
        Assert.NotNull(session);
        Assert.Equal(seed.LocationId, session!.BusinessLocationId);

        var clockOut = await client.PostAsJsonAsync(
            "/api/v1/tenant/attendance/me/events",
            new RecordStaffAttendanceEventRequest("clock_out", seed.LocationId));
        Assert.Equal(HttpStatusCode.Created, clockOut.StatusCode);

        var after = await client.GetAsync("/api/v1/tenant/presence/me");
        Assert.True(
            after.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
            $"Unexpected status: {after.StatusCode}");
        if (after.StatusCode == HttpStatusCode.OK)
        {
            var cleared = await after.Content.ReadFromJsonAsync<VerifiedPresenceSessionResponse?>();
            Assert.Null(cleared);
        }
    }

    [Fact]
    public async Task SensitivePolicy_BlocksCustomersWithoutPresence()
    {
        var seed = await SeedAsync(geofenceEnabled: false);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.LocationSecurityPolicies.Add(new LocationSecurityPolicy
        {
            TenantId = seed.TenantId,
            RequirePresenceForSensitiveCustomerData = true,
            PresenceValidityMinutes = 90,
            AllowManagerPresenceOverride = true,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        using var client = Client(seed.ManagerSub, seed.TenantId);
        var response = await client.GetAsync("/api/v1/tenant/customers");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("staff.presence_required", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ScheduleToday_ReturnsForLinkedStaff()
    {
        var seed = await SeedAsync(geofenceEnabled: false);
        using var client = Client(seed.PractitionerSub, seed.TenantId);
        var response = await client.GetAsync("/api/v1/tenant/attendance/me/schedule-today");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(seed.StaffMemberId.ToString(), body.GetProperty("staffMemberId").GetString());
    }

    [Fact]
    public async Task StaffHistory_ManagerCanLoadRange()
    {
        var seed = await SeedAsync(geofenceEnabled: false);
        using var practitioner = Client(seed.PractitionerSub, seed.TenantId);
        await practitioner.PostAsJsonAsync(
            "/api/v1/tenant/attendance/me/events",
            new RecordStaffAttendanceEventRequest("clock_in", seed.LocationId));

        using var manager = Client(seed.ManagerSub, seed.TenantId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var response = await manager.GetAsync(
            $"/api/v1/tenant/attendance/staff/{seed.StaffMemberId}?from={today}&to={today}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("events").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task StaffCorrection_AppendsAuditEvent()
    {
        var seed = await SeedAsync(geofenceEnabled: false);
        using var practitioner = Client(seed.PractitionerSub, seed.TenantId);
        var clockIn = await practitioner.PostAsJsonAsync(
            "/api/v1/tenant/attendance/me/events",
            new RecordStaffAttendanceEventRequest("clock_in", seed.LocationId));
        Assert.Equal(HttpStatusCode.Created, clockIn.StatusCode);
        var clockInBody = await clockIn.Content.ReadFromJsonAsync<JsonElement>();
        var eventId = clockInBody.GetProperty("id").GetGuid();

        using var manager = Client(seed.ManagerSub, seed.TenantId);
        var response = await manager.PostAsJsonAsync(
            $"/api/v1/tenant/attendance/staff/{seed.StaffMemberId}/corrections",
            new Adeni.Application.Booking.AppendStaffAttendanceCorrectionRequest(
                eventId,
                "Corrected mistaken clock-in time with reception."));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ExplicitVerify_RequiresOnShift()
    {
        var seed = await SeedAsync(geofenceEnabled: false);
        using var client = Client(seed.PractitionerSub, seed.TenantId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/tenant/presence/verify",
            new VerifyPresenceRequest(seed.LocationId));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
