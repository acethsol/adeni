namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Adeni.Api.Middleware;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class CustomerSegmentsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    public CustomerSegmentsIntegrationTests(WebApplicationFactory<Program> factory) => this.factory = factory.WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Testing");
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:AdeniDb"] = "", ["Redis:ConnectionString"] = "", ["Auth0:Enabled"] = "false" }));
    });

    private async Task<(HttpClient Client, Guid Tenant, Guid Roster, Guid Customer, Guid Location)> Seed(string role = "owner", SubscriptionSku sku = SubscriptionSku.Professional, bool erased = false)
    {
        var tenant = Guid.NewGuid(); var roster = Guid.NewGuid(); var customer = Guid.NewGuid(); var location = Guid.NewGuid();
        var sub = $"segments-{tenant:N}";
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.Tenants.Add(new Tenant { Id = tenant, Name = "Segments", Status = TenantStatus.Verified, SubscriptionSku = sku, BillingStatus = TenantBillingStatus.Active });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Auth0Sub = sub, Role = role });
        db.BusinessLocations.Add(new BusinessLocation { Id = location, TenantId = tenant, Name = "Main", Slug = location.ToString(), IsActive = true, IsPrimary = true, TimeZoneId = "Africa/Lagos" });
        db.Customers.Add(new Customer { Id = customer, Auth0Sub = customer.ToString(), ErasureRequestedAt = erased ? DateTimeOffset.UtcNow : null });
        db.TenantCustomers.Add(new TenantCustomer { Id = roster, TenantId = tenant, CustomerId = customer, DisplayName = "Ada" });
        var service = new ServiceOffering { Id = Guid.NewGuid(), TenantId = tenant, Name = "Care" }; db.ServiceOfferings.Add(service);
        db.Bookings.Add(new BookingRecord { Id = Guid.NewGuid(), TenantId = tenant, CustomerId = customer, BusinessLocationId = location,
            ServiceOfferingId = service.Id, StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(-1).AddHours(1), Status = BookingStatus.Completed });
        await db.SaveChangesAsync(); var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, sub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenant.ToString());
        return (client, tenant, roster, customer, location);
    }

    [Theory]
    [InlineData("owner", HttpStatusCode.OK)]
    [InlineData("manager", HttpStatusCode.OK)]
    [InlineData("receptionist", HttpStatusCode.OK)]
    [InlineData("practitioner", HttpStatusCode.Forbidden)]
    [InlineData("accountant", HttpStatusCode.Forbidden)]
    [InlineData("legacy-unknown", HttpStatusCode.Forbidden)]
    public async Task Segments_RequireRecognizedTenantRoleAndFullBookingAccess(string role, HttpStatusCode expected)
    {
        var data = await Seed(role); using var client = data.Client;
        var response = await client.GetAsync($"/api/v1/tenant/customers/{data.Roster}/segments");
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("available", json.GetProperty("availability").GetString());
            Assert.Equal(2, json.GetProperty("matches").GetArrayLength());
            Assert.Equal(data.Location, json.GetProperty("locationId").GetGuid());
            Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        }
    }

    [Theory]
    [InlineData(SubscriptionSku.Starter, false, "notEntitled")]
    [InlineData(SubscriptionSku.Professional, true, "erased")]
    public async Task Segments_EntitlementAndErasureDoNotReturnPlaceholderLabels(SubscriptionSku sku, bool erased, string expected)
    {
        var data = await Seed(sku: sku, erased: erased); using var client = data.Client;
        var json = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tenant/customers/{data.Roster}/segments");
        Assert.Equal(expected, json.GetProperty("availability").GetString()); Assert.Empty(json.GetProperty("matches").EnumerateArray());
    }

    [Fact]
    public async Task Batch_RejectsForeignDuplicatesEmptyAndOversizedRequests()
    {
        var data = await Seed(); var foreign = await Seed(); using var client = data.Client; using var other = foreign.Client;
        foreach (var ids in new[] { Array.Empty<Guid>(), new[] { data.Roster, data.Roster }, Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray() })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/tenant/customers/segments/batch", new { customerIds = ids })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/tenant/customers/segments/batch", new { customerIds = new[] { foreign.Roster } })).StatusCode);
        var ok = await client.PostAsJsonAsync("/api/v1/tenant/customers/segments/batch", new { customerIds = new[] { data.Roster } }); Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/tenant/customers/{data.Roster}/segments?locationId={foreign.Location}")).StatusCode);
        client.DefaultRequestHeaders.Remove(TenantAccessMiddleware.TenantHeaderName); client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, foreign.Tenant.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/tenant/customers/{foreign.Roster}/segments")).StatusCode);
    }
    [Theory]
    [InlineData(SubscriptionSku.Professional, 1)]
    [InlineData(SubscriptionSku.Business, 2)]
    public async Task Scope_UsesF1ResolutionAndDoesNotConsultHiddenBranchUpcomingBookings(SubscriptionSku sku, int expected)
    {
        var data = await Seed(sku: sku); using var client = data.Client; var other = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            db.BusinessLocations.Add(new BusinessLocation { Id = other, TenantId = data.Tenant, Slug = other.ToString(), IsActive = true, TimeZoneId = "Africa/Lagos" });
            var booking = db.Bookings.Single(x => x.TenantId == data.Tenant); booking.StartAt = DateTimeOffset.UtcNow.AddDays(-100);
            db.Bookings.Add(new BookingRecord { Id = Guid.NewGuid(), TenantId = data.Tenant, CustomerId = data.Customer, ServiceOfferingId = booking.ServiceOfferingId,
                BusinessLocationId = other, StartAt = DateTimeOffset.UtcNow.AddDays(-1), Status = BookingStatus.Completed });
            db.Bookings.Add(new BookingRecord { Id = Guid.NewGuid(), TenantId = data.Tenant, CustomerId = data.Customer, ServiceOfferingId = booking.ServiceOfferingId,
                BusinessLocationId = other, StartAt = DateTimeOffset.UtcNow.AddDays(1), Status = BookingStatus.Confirmed });
            await db.SaveChangesAsync();
        }
        var row = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tenant/customers/{data.Roster}/segments");
        var labels = row.GetProperty("matches").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray();
        Assert.Contains(expected == 1 ? "lapsed" : "recentlyActive", labels);
        var branch = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tenant/customers/{data.Roster}/segments?locationId={data.Location}");
        Assert.Contains(branch.GetProperty("matches").EnumerateArray(), x => x.GetProperty("id").GetString() == "lapsed");
    }

    [Fact]
    public async Task PrivacyExport_IncludesExplainableCurrentClassifications_AndErasurePreventsRederivation()
    {
        var data = await Seed(); using var client = data.Client;
        using var scope = factory.Services.CreateScope();
        var segments = scope.ServiceProvider.GetRequiredService<Adeni.Application.Tenancy.ICustomerSegmentsQueryService>();
        var before = Assert.Single(await segments.ExportAsync(data.Customer, default)); Assert.Equal(2, before.Matches.Count);
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>(); db.Customers.Single(x => x.Id == data.Customer).ErasureRequestedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(); var after = Assert.Single(await segments.ExportAsync(data.Customer, default));
        Assert.Equal("erased", after.Availability); Assert.Empty(after.Matches);
        var serialized = JsonSerializer.Serialize(after); Assert.DoesNotContain("Ada", serialized); Assert.DoesNotContain("Answer", serialized);
    }

    // GiftMetadata_* (purchased-gift-cards scope rules) live on feature/f3-gift-cards-pay-links — see PR #40.
    // F5 segment tests above do not depend on F3 gift-card routes.
}
