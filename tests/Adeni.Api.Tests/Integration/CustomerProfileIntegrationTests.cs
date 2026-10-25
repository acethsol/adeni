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

public sealed class CustomerProfileIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    public CustomerProfileIntegrationTests(WebApplicationFactory<Program> factory) => this.factory = factory.WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AdeniDb"] = "", ["Redis:ConnectionString"] = "", ["Auth0:Enabled"] = "false",
        }));
    });

    private async Task<(HttpClient Client, Guid TenantId, Guid CustomerId)> SeedAsync(string role = "owner", SubscriptionSku sku = SubscriptionSku.Starter, TenantBillingStatus status = TenantBillingStatus.Active)
    {
        var tenantId = Guid.NewGuid();
        var rosterId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var sub = $"auth0|profile-{tenantId:N}";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Profile tests", Status = TenantStatus.Verified,
            SubscriptionSku = sku, BillingStatus = status, CreatedAt = DateTimeOffset.UtcNow,
            TrialSku = status == TenantBillingStatus.Trialing ? SubscriptionSku.Professional : null,
            TrialEndsAt = status == TenantBillingStatus.Trialing ? DateTimeOffset.UtcNow.AddDays(7) : null });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenantId, Auth0Sub = sub, Role = role });
        db.Customers.Add(new Customer { Id = platformId, Auth0Sub = $"customer-{platformId:N}", Name = "Ada" });
        db.TenantCustomers.Add(new TenantCustomer { Id = rosterId, TenantId = tenantId, CustomerId = platformId, DisplayName = "Ada" });
        var serviceId = Guid.NewGuid();
        db.ServiceOfferings.Add(new ServiceOffering { Id = serviceId, TenantId = tenantId, Name = "Hair care", Currency = "NGN" });
        db.Bookings.AddRange(Enumerable.Range(1, 30).Select(i => new BookingRecord
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CustomerId = platformId, ServiceOfferingId = serviceId,
            StartAt = DateTimeOffset.UtcNow.AddDays(-i), EndAt = DateTimeOffset.UtcNow.AddDays(-i).AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-i - 1), Status = BookingStatus.Completed,
        }));
        await db.SaveChangesAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, sub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());
        return (client, tenantId, rosterId);
    }

    [Theory]
    [InlineData("owner", HttpStatusCode.OK, "available")]
    [InlineData("manager", HttpStatusCode.OK, "available")]
    [InlineData("receptionist", HttpStatusCode.OK, "restricted")]
    [InlineData("practitioner", HttpStatusCode.Forbidden, null)]
    [InlineData("accountant", HttpStatusCode.Forbidden, null)]
    [InlineData("ops", HttpStatusCode.Forbidden, null)]
    public async Task Profile_EnforcesCustomerAndSourcePermissions(string role, HttpStatusCode expected, string? finance)
    {
        var data = await SeedAsync(role);
        using var client = data.Client;
        var result = await client.GetAsync($"/api/v1/tenant/customers/{data.CustomerId}/profile");
        Assert.Equal(expected, result.StatusCode);
        if (expected != HttpStatusCode.OK) return;
        var profile = (await result.Content.ReadFromJsonAsync<JsonElement>());
        Assert.Equal(finance, profile.GetProperty("availability").GetProperty("finance").GetString());
        Assert.Equal(30, profile.GetProperty("visitSummary").GetProperty("completedBookings").GetInt32());
        if (finance == "restricted") Assert.Equal(JsonValueKind.Null, profile.GetProperty("recordedOnlinePaymentsByCurrency").ValueKind);
    }

    [Theory]
    [InlineData(SubscriptionSku.Starter, TenantBillingStatus.Active, 3)]
    [InlineData(SubscriptionSku.Professional, TenantBillingStatus.Active, 25)]
    [InlineData(SubscriptionSku.Business, TenantBillingStatus.Active, 25)]
    [InlineData(SubscriptionSku.Business, TenantBillingStatus.Suspended, 3)]
    [InlineData(SubscriptionSku.Business, TenantBillingStatus.Canceled, 3)]
    [InlineData(SubscriptionSku.Starter, TenantBillingStatus.Trialing, 25)]
    public async Task Activity_PagingCannotBypassEffectivePlanHistoryLimits(SubscriptionSku sku, TenantBillingStatus billing, int expectedBookings)
    {
        var data = await SeedAsync(sku: sku, status: billing);
        using var client = data.Client;
        var ids = new HashSet<string>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var suffix = cursor is null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}";
            var response = await client.GetAsync($"/api/v1/tenant/customers/{data.CustomerId}/activity?pageSize=2{suffix}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            foreach (var item in body.GetProperty("items").EnumerateArray()) ids.Add(item.GetProperty("sourceId").GetString()!);
            cursor = body.GetProperty("nextCursor").GetString();
            Assert.True(++pages < 40);
        } while (cursor is not null);
        Assert.Equal(expectedBookings, ids.Count);
    }

    [Fact]
    public async Task Profile_CrossTenantCustomerAndHeaders_DoNotExposeData()
    {
        var a = await SeedAsync();
        var b = await SeedAsync();
        using var client = a.Client;
        using var other = b.Client;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/tenant/customers/{b.CustomerId}/profile")).StatusCode);
        client.DefaultRequestHeaders.Remove(TenantAccessMiddleware.TenantHeaderName);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, b.TenantId.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/tenant/customers/{b.CustomerId}/profile")).StatusCode);
    }

    [Fact]
    public async Task Profile_AnonymousAndInvalidInputs_FailClosed()
    {
        var data = await SeedAsync();
        using var client = data.Client;
        var path = $"/api/v1/tenant/customers/{data.CustomerId}";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{path}/activity?pageSize=51")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{path}/activity?cursor=forged")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{path}/profile?locationId={Guid.NewGuid()}")).StatusCode);
        client.DefaultRequestHeaders.Remove(DevBusinessAuthMiddleware.DevAuth0SubHeader);
        Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync($"{path}/profile")).StatusCode);
    }

    [Fact]
    public async Task ExistingCustomerDetail_StillHasOriginalContract()
    {
        var data = await SeedAsync();
        using var client = data.Client;
        var response = await client.GetAsync($"/api/v1/tenant/customers/{data.CustomerId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetProperty("recentBookings").GetArrayLength());
        Assert.True(body.TryGetProperty("recentQueueVisits", out _));
        Assert.False(body.TryGetProperty("recordedOnlinePaymentsByCurrency", out _));
    }

    [Fact]
    public async Task Practitioner_ExistingDetailPolicyRemainsUnchanged_NewAggregateIsDeferred()
    {
        var data = await SeedAsync("practitioner");
        using var client = data.Client;
        var path = $"/api/v1/tenant/customers/{data.CustomerId}";
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{path}/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{path}/activity")).StatusCode);
    }

    [Theory]
    [InlineData(SubscriptionSku.Starter, 1)]
    [InlineData(SubscriptionSku.Business, 30)]
    public async Task Profile_LocationAggregation_UsesExistingEntitlementScope(SubscriptionSku sku, int expectedVisits)
    {
        var data = await SeedAsync(sku: sku);
        using var client = data.Client;
        var primary = Guid.NewGuid();
        var secondary = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            db.BusinessLocations.AddRange(new BusinessLocation { Id = primary, TenantId = data.TenantId, IsActive = true, IsPrimary = true, Slug = primary.ToString() },
                new BusinessLocation { Id = secondary, TenantId = data.TenantId, IsActive = true, Slug = secondary.ToString() });
            var bookings = db.Bookings.Where(x => x.TenantId == data.TenantId).ToArray();
            for (var i = 0; i < bookings.Length; i++) bookings[i].BusinessLocationId = i == 0 ? primary : secondary;
            await db.SaveChangesAsync();
        }
        var body = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tenant/customers/{data.CustomerId}/profile");
        Assert.Equal(expectedVisits, body.GetProperty("visitSummary").GetProperty("completedBookings").GetInt32());
        var explicitBranch = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tenant/customers/{data.CustomerId}/profile?locationId={secondary}");
        Assert.Equal(29, explicitBranch.GetProperty("visitSummary").GetProperty("completedBookings").GetInt32());
        Assert.Equal("unscoped", explicitBranch.GetProperty("availability").GetProperty("messaging").GetString());
    }
}
