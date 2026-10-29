namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Retention;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class CustomerMembershipsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Retention = "/api/v1/tenant/retention";
    private readonly WebApplicationFactory<Program> factory;

    public CustomerMembershipsIntegrationTests(WebApplicationFactory<Program> factory) =>
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty,
                    ["Redis:ConnectionString"] = string.Empty,
                }));
        });

    private sealed record Seeded(
        Guid Tenant, Guid Location, Guid Service, Guid Customer, string Owner, string Manager, string Accountant);

    private async Task<Seeded> SeedAsync(SubscriptionSku sku = SubscriptionSku.Professional)
    {
        var tenant = Guid.NewGuid();
        var location = Guid.NewGuid();
        var service = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var owner = $"owner-{tenant:N}";
        var manager = $"manager-{tenant:N}";
        var accountant = $"accountant-{tenant:N}";
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = tenant, Name = "Membership Salon", Status = TenantStatus.Verified,
            SubscriptionSku = sku, BillingStatus = TenantBillingStatus.Active, CreatedAt = now, VerifiedAt = now,
        });
        db.Add(new BusinessLocation { Id = location, TenantId = tenant, Name = "Main", Slug = "main", IsPrimary = true, CreatedAt = now });
        db.ServiceOfferings.Add(new ServiceOffering
        {
            Id = service, TenantId = tenant, Name = "Haircut", PriceAmount = 4000m, Currency = "NGN",
            DurationMinutes = 30, IsActive = true, CreatedAt = now, UpdatedAt = now,
        });
        db.Add(new TenantCustomer
        {
            Id = customer, TenantId = tenant, CustomerId = Guid.NewGuid(), DisplayName = "Ada",
            CreatedAt = now, UpdatedAt = now, LastSeenAt = now,
        });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Auth0Sub = owner, Role = "owner" });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Auth0Sub = manager, Role = "manager" });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Auth0Sub = accountant, Role = "accountant" });
        await db.SaveChangesAsync();
        return new Seeded(tenant, location, service, customer, owner, manager, accountant);
    }

    private HttpClient Client(string? sub = null, Guid? tenant = null)
    {
        var client = factory.CreateClient();
        if (sub is not null)
        {
            client.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub", sub);
        }

        if (tenant is not null)
        {
            client.DefaultRequestHeaders.Add("X-Tenant-Id", tenant.ToString());
        }

        return client;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<Guid> CreatePlanAsync(Seeded s, HttpClient owner, bool allowFreeze = true, string cancellation = "end_of_cycle")
    {
        var response = await owner.PostAsJsonAsync($"{Retention}/memberships/plans", new
        {
            name = "Fade Club", price = 25000, currency = "NGN", billingInterval = "monthly",
            allowFreeze, cancellationPolicy = cancellation,
            benefits = new[] { new { serviceOfferingId = s.Service, quantityPerCycle = 2 } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private static HttpRequestMessage EnrolRequest(Seeded s, Guid plan, string? key, string method = "till")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{Retention}/customers/{s.Customer}/memberships/enrol")
        {
            Content = JsonContent.Create(new { membershipPlanId = plan, businessLocationId = s.Location, paymentMethod = method }),
        };
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return request;
    }

    [Fact]
    public async Task Memberships_ManagerEnrols_ThenListsGetsAndOwnerFreezesAndCancels()
    {
        var s = await SeedAsync();
        using var owner = Client(s.Owner, s.Tenant);
        using var manager = Client(s.Manager, s.Tenant);
        var plan = await CreatePlanAsync(s, owner);

        var enrolled = await manager.SendAsync(EnrolRequest(s, plan, "enrol-1"));
        Assert.Equal(HttpStatusCode.Created, enrolled.StatusCode);
        var membership = await ReadAsync(enrolled);
        var id = membership.GetProperty("id").GetGuid();
        Assert.Equal("active", membership.GetProperty("status").GetString());
        Assert.Equal(2, membership.GetProperty("balances")[0].GetProperty("remaining").GetInt32());

        var list = await manager.GetFromJsonAsync<JsonElement>($"{Retention}/customers/{s.Customer}/memberships");
        Assert.Equal(1, list.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"{Retention}/customer-memberships/{id}")).StatusCode);

        // Freeze / cancel are owner-only.
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"{Retention}/customer-memberships/{id}/freeze", null)).StatusCode);

        var frozen = await owner.PostAsync($"{Retention}/customer-memberships/{id}/freeze", null);
        Assert.Equal(HttpStatusCode.OK, frozen.StatusCode);
        Assert.Equal("frozen", (await ReadAsync(frozen)).GetProperty("status").GetString());

        var thawed = await owner.PostAsync($"{Retention}/customer-memberships/{id}/unfreeze", null);
        Assert.Equal("active", (await ReadAsync(thawed)).GetProperty("status").GetString());

        var cancelled = await owner.PostAsJsonAsync($"{Retention}/customer-memberships/{id}/cancel", new { reason = "moved away" });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("cancel_pending", (await ReadAsync(cancelled)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Enrol_RequiresIdempotencyKey_AndReplaysSameKey()
    {
        var s = await SeedAsync();
        using var owner = Client(s.Owner, s.Tenant);
        var plan = await CreatePlanAsync(s, owner);

        var missing = await owner.SendAsync(EnrolRequest(s, plan, null));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("retention.idempotency_key_required", await missing.Content.ReadAsStringAsync());

        var first = await ReadAsync(await owner.SendAsync(EnrolRequest(s, plan, "same-key")));
        var replay = await owner.SendAsync(EnrolRequest(s, plan, "same-key"));
        Assert.Equal(first.GetProperty("id").GetGuid(), (await ReadAsync(replay)).GetProperty("id").GetGuid());

        var conflict = await owner.SendAsync(EnrolRequest(s, plan, "same-key", method: "transfer"));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("retention.idempotency_conflict", await conflict.Content.ReadAsStringAsync());

        var second = await owner.SendAsync(EnrolRequest(s, plan, "other-key"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("retention.membership_already_active", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Freeze_WhenPlanDisallows_Returns409WithStableCode()
    {
        var s = await SeedAsync();
        using var owner = Client(s.Owner, s.Tenant);
        var plan = await CreatePlanAsync(s, owner, allowFreeze: false);
        var id = (await ReadAsync(await owner.SendAsync(EnrolRequest(s, plan, "k")))).GetProperty("id").GetGuid();

        var response = await owner.PostAsync($"{Retention}/customer-memberships/{id}/freeze", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("retention.membership_freeze_not_allowed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Memberships_StarterTenant_IsDeniedWithEntitlementCode()
    {
        var s = await SeedAsync(SubscriptionSku.Starter);
        using var owner = Client(s.Owner, s.Tenant);

        var list = await owner.GetAsync($"{Retention}/customers/{s.Customer}/memberships");
        var enrol = await owner.SendAsync(EnrolRequest(s, Guid.NewGuid(), "k"));

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, enrol.StatusCode);
        Assert.Contains("entitlements.memberships", await enrol.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Memberships_RequireAuth_AndRetentionPermissionToEnrol()
    {
        var s = await SeedAsync();
        using var owner = Client(s.Owner, s.Tenant);
        var plan = await CreatePlanAsync(s, owner);

        using var anon = Client(tenant: s.Tenant);
        Assert.Contains(
            (await anon.SendAsync(EnrolRequest(s, plan, "k"))).StatusCode,
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });

        using var accountant = Client(s.Accountant, s.Tenant);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.SendAsync(EnrolRequest(s, plan, "k"))).StatusCode);
    }

    [Fact]
    public async Task Memberships_CrossTenant_AreDeniedAndInvisible()
    {
        var a = await SeedAsync();
        var b = await SeedAsync();
        using var ownerA = Client(a.Owner, a.Tenant);
        var plan = await CreatePlanAsync(a, ownerA);
        var id = (await ReadAsync(await ownerA.SendAsync(EnrolRequest(a, plan, "k")))).GetProperty("id").GetGuid();

        using var crossHeader = Client(a.Owner, b.Tenant);
        Assert.Equal(HttpStatusCode.Forbidden, (await crossHeader.GetAsync($"{Retention}/customer-memberships/{id}")).StatusCode);

        using var ownerB = Client(b.Owner, b.Tenant);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.GetAsync($"{Retention}/customer-memberships/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.PostAsync($"{Retention}/customer-memberships/{id}/freeze", null)).StatusCode);
        // Tenant B cannot enrol tenant A's customer or use tenant A's plan.
        var crossEnrol = await ownerB.SendAsync(EnrolRequest(a, plan, "x"));
        Assert.Equal(HttpStatusCode.NotFound, crossEnrol.StatusCode);
    }
}
