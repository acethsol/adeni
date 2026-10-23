namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Api.Middleware;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class TenantDashboardIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TenantDashboardIntegrationTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty,
                    ["Redis:ConnectionString"] = string.Empty,
                    ["Auth0:Enabled"] = "false",
                });
            });
        });

    [Fact]
    public async Task Summary_Returns_pending_and_appointments_for_range()
    {
        var tenantId = Guid.NewGuid();
        const string ownerSub = "auth0|dashboard-owner";
        var startAt = DateTimeOffset.UtcNow.AddHours(2);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, ownerSub);
            var serviceId = Guid.NewGuid();
            db.ServiceOfferings.Add(new ServiceOffering
            {
                Id = serviceId,
                TenantId = tenantId,
                Name = "Cut",
                PriceAmount = 3000m,
                Currency = "NGN",
                DurationMinutes = 30,
                IsActive = true,
                CreatedAt = startAt,
                UpdatedAt = startAt,
            });
            db.Bookings.Add(new BookingRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceOfferingId = serviceId,
                StartAt = startAt,
                EndAt = startAt.AddMinutes(30),
                Status = BookingStatus.Pending,
                CreatedAt = startAt,
                UpdatedAt = startAt,
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var response = await client.GetAsync($"/api/v1/tenant/dashboard/summary?from={from}&to={to}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryPayload>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.PendingCount);
        Assert.Equal(1, payload.AppointmentsCount);
        Assert.False(payload.AnalyticsIncluded);
    }

    [Fact]
    public async Task Summary_filters_by_locationId()
    {
        var tenantId = Guid.NewGuid();
        const string ownerSub = "auth0|dashboard-loc-filter";
        var locA = Guid.NewGuid();
        var locB = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var startAt = DateTimeOffset.UtcNow.AddHours(2);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, ownerSub);
            db.Customers.Add(new Customer
            {
                Id = customerId,
                Auth0Sub = "auth0|dash-cust",
                Name = "Dash Customer",
                Phone = "+2348022222222",
                CreatedAt = startAt,
            });
            db.ServiceOfferings.Add(new ServiceOffering
            {
                Id = serviceId,
                TenantId = tenantId,
                Name = "Cut",
                PriceAmount = 3000m,
                Currency = "NGN",
                DurationMinutes = 30,
                IsActive = true,
                CreatedAt = startAt,
                UpdatedAt = startAt,
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = locA,
                TenantId = tenantId,
                Name = "A",
                Slug = "dash-loc-a",
                MarketId = "lagos",
                AddressLine = "1",
                Area = "A",
                IsPrimary = true,
                IsActive = true,
                CreatedAt = startAt,
                UpdatedAt = startAt,
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = locB,
                TenantId = tenantId,
                Name = "B",
                Slug = "dash-loc-b",
                MarketId = "lagos",
                AddressLine = "2",
                Area = "B",
                IsPrimary = false,
                IsActive = true,
                CreatedAt = startAt,
                UpdatedAt = startAt,
            });
            db.Bookings.Add(new BookingRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceOfferingId = serviceId,
                CustomerId = customerId,
                BusinessLocationId = locA,
                StartAt = startAt,
                EndAt = startAt.AddMinutes(30),
                Status = BookingStatus.Pending,
                CreatedAt = startAt,
                UpdatedAt = startAt,
            });
            db.Bookings.Add(new BookingRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceOfferingId = serviceId,
                CustomerId = customerId,
                BusinessLocationId = locB,
                StartAt = startAt.AddHours(1),
                EndAt = startAt.AddHours(1).AddMinutes(30),
                Status = BookingStatus.Pending,
                CreatedAt = startAt,
                UpdatedAt = startAt,
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var allResponse = await client.GetAsync($"/api/v1/tenant/dashboard/summary?from={from}&to={to}");
        var scopedResponse = await client.GetAsync(
            $"/api/v1/tenant/dashboard/summary?from={from}&to={to}&locationId={locA:D}");

        Assert.Equal(HttpStatusCode.OK, allResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, scopedResponse.StatusCode);
        var allPayload = await allResponse.Content.ReadFromJsonAsync<DashboardSummaryPayload>();
        var scopedPayload = await scopedResponse.Content.ReadFromJsonAsync<DashboardSummaryPayload>();
        Assert.NotNull(allPayload);
        Assert.NotNull(scopedPayload);
        // Starter tier: omitted locationId defaults to primary branch when 2+ locations.
        Assert.Equal(1, allPayload!.PendingCount);
        Assert.Equal(1, scopedPayload!.PendingCount);
        var locBResponse = await client.GetAsync(
            $"/api/v1/tenant/dashboard/summary?from={from}&to={to}&locationId={locB:D}");
        var locBPayload = await locBResponse.Content.ReadFromJsonAsync<DashboardSummaryPayload>();
        Assert.NotNull(locBPayload);
        Assert.Equal(1, locBPayload!.PendingCount);
    }

    [Fact]
    public async Task Subscription_usage_filters_by_locationId()
    {
        var tenantId = Guid.NewGuid();
        const string ownerSub = "auth0|dashboard-sub-loc";
        var locA = Guid.NewGuid();
        var locB = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, ownerSub);
            db.ServiceOfferings.Add(new ServiceOffering
            {
                Id = serviceId,
                TenantId = tenantId,
                Name = "Cut",
                PriceAmount = 3000m,
                Currency = "NGN",
                DurationMinutes = 30,
                IsActive = true,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = locA,
                TenantId = tenantId,
                Name = "A",
                Slug = "sub-loc-a",
                MarketId = "lagos",
                AddressLine = "1",
                Area = "A",
                IsPrimary = true,
                IsActive = true,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = locB,
                TenantId = tenantId,
                Name = "B",
                Slug = "sub-loc-b",
                MarketId = "lagos",
                AddressLine = "2",
                Area = "B",
                IsPrimary = false,
                IsActive = true,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            });
            db.Bookings.Add(new BookingRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceOfferingId = serviceId,
                BusinessLocationId = locA,
                StartAt = createdAt.AddHours(1),
                EndAt = createdAt.AddHours(1).AddMinutes(30),
                Status = BookingStatus.Confirmed,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            });
            db.Bookings.Add(new BookingRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceOfferingId = serviceId,
                BusinessLocationId = locB,
                StartAt = createdAt.AddHours(2),
                EndAt = createdAt.AddHours(2).AddMinutes(30),
                Status = BookingStatus.Confirmed,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var allResponse = await client.GetAsync("/api/v1/tenant/subscription");
        var scopedResponse = await client.GetAsync(
            $"/api/v1/tenant/subscription?locationId={locA:D}");

        Assert.Equal(HttpStatusCode.OK, allResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, scopedResponse.StatusCode);
        var allPayload = await allResponse.Content.ReadFromJsonAsync<SubscriptionUsagePayload>();
        var scopedPayload = await scopedResponse.Content.ReadFromJsonAsync<SubscriptionUsagePayload>();
        Assert.NotNull(allPayload);
        Assert.NotNull(scopedPayload);
        Assert.Equal(1, allPayload!.BookingsUsedThisMonth);
        Assert.Equal(1, scopedPayload!.BookingsUsedThisMonth);
        var locBResponse = await client.GetAsync(
            $"/api/v1/tenant/subscription?locationId={locB:D}");
        var locBPayload = await locBResponse.Content.ReadFromJsonAsync<SubscriptionUsagePayload>();
        Assert.NotNull(locBPayload);
        Assert.Equal(1, locBPayload!.BookingsUsedThisMonth);
    }

    [Fact]
    public async Task Series_appointments_allowed_without_analytics()
    {
        var tenantId = Guid.NewGuid();
        const string ownerSub = "auth0|dashboard-series";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, ownerSub);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var response = await client.GetAsync(
            "/api/v1/tenant/dashboard/series?metric=appointments&from=2026-01-01T00:00:00Z&to=2026-02-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Series_collected_requires_analytics()
    {
        var tenantId = Guid.NewGuid();
        const string ownerSub = "auth0|dashboard-collected";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, ownerSub);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var response = await client.GetAsync(
            "/api/v1/tenant/dashboard/series?metric=collected&from=2026-01-01T00:00:00Z&to=2026-02-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("entitlements.analytics_required", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Layout_save_and_reload_roundtrip()
    {
        var tenantId = Guid.NewGuid();
        var businessUserId = Guid.NewGuid();
        const string ownerSub = "auth0|dashboard-layout";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenant(db, tenantId, ownerSub, businessUserId);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var customLayout = new
        {
            version = 1,
            range = "30d",
            widgets = new[]
            {
                new { id = "kpis_row", w = 12 },
                new { id = "quick_actions", w = 12 },
            },
        };

        var putResponse = await client.PutAsJsonAsync("/api/v1/tenant/dashboard/layout", customLayout);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/v1/tenant/dashboard/layout");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var payload = await getResponse.Content.ReadFromJsonAsync<LayoutPayload>();
        Assert.NotNull(payload);
        Assert.Equal("30d", payload!.Range);
        Assert.Equal(2, payload.Widgets.Length);
        Assert.Equal("kpis_row", payload.Widgets[0].Id);
    }

    private static void SeedTenant(
        AdeniDbContext db,
        Guid tenantId,
        string auth0Sub,
        Guid? businessUserId = null)
    {
        var now = DateTimeOffset.UtcNow;
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Dashboard Test",
            Status = TenantStatus.Verified,
            CreatedAt = now,
            VerifiedAt = now,
        });
        db.BusinessUsers.Add(new BusinessUser
        {
            Id = businessUserId ?? Guid.NewGuid(),
            TenantId = tenantId,
            Auth0Sub = auth0Sub,
            Role = "owner",
            CreatedAt = now,
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "barbers",
            Phone = "+2348012345678",
            UpdatedAt = now,
        });
    }

    private sealed record DashboardSummaryPayload(
        int PendingCount,
        int AppointmentsCount,
        bool AnalyticsIncluded);

    private sealed record SubscriptionUsagePayload(int BookingsUsedThisMonth);

    private sealed record LayoutWidgetPayload(string Id, int W);

    private sealed record LayoutPayload(int Version, string Range, LayoutWidgetPayload[] Widgets);
}
