namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class MembershipPlansIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Route = "/api/v1/tenant/retention/memberships/plans";
    private readonly WebApplicationFactory<Program> factory;

    public MembershipPlansIntegrationTests(WebApplicationFactory<Program> factory) =>
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

    private sealed record Seeded(Guid Tenant, Guid Service, string Owner, string Manager, string Accountant);

    private async Task<Seeded> SeedAsync(SubscriptionSku sku = SubscriptionSku.Professional)
    {
        var tenant = Guid.NewGuid();
        var service = Guid.NewGuid();
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
        db.ServiceOfferings.Add(new ServiceOffering
        {
            Id = service, TenantId = tenant, Name = "Haircut", PriceAmount = 4000m, Currency = "NGN",
            DurationMinutes = 30, IsActive = true, CreatedAt = now, UpdatedAt = now,
        });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Auth0Sub = owner, Role = "owner" });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Auth0Sub = manager, Role = "manager" });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Auth0Sub = accountant, Role = "accountant" });
        await db.SaveChangesAsync();
        return new Seeded(tenant, service, owner, manager, accountant);
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

    private static object PlanBody(Seeded s, string name = "Fade Club") => new
    {
        name,
        price = 25000,
        currency = "NGN",
        billingInterval = "monthly",
        allowFreeze = true,
        benefits = new[] { new { serviceOfferingId = s.Service, quantityPerCycle = 2 } },
    };

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Plans_OwnerCanCreateListGetPatchAndDeactivate()
    {
        var s = await SeedAsync();
        using var owner = Client(s.Owner, s.Tenant);

        var created = await owner.PostAsJsonAsync(Route, PlanBody(s));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var plan = await ReadAsync(created);
        var id = plan.GetProperty("id").GetGuid();
        Assert.Equal("monthly", plan.GetProperty("billingInterval").GetString());
        Assert.Equal(2, plan.GetProperty("benefits")[0].GetProperty("quantityPerCycle").GetInt32());

        var list = await owner.GetFromJsonAsync<JsonElement>(Route);
        Assert.Equal(1, list.GetProperty("items").GetArrayLength());

        var get = await owner.GetAsync($"{Route}/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var patch = await owner.PatchAsJsonAsync($"{Route}/{id}", new { price = 30000, billingInterval = "quarterly" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var patched = await ReadAsync(patch);
        Assert.Equal(30000m, patched.GetProperty("price").GetDecimal());
        Assert.Equal("quarterly", patched.GetProperty("billingInterval").GetString());
        Assert.Equal("Fade Club", patched.GetProperty("name").GetString());

        var deactivated = await owner.PostAsync($"{Route}/{id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.False((await ReadAsync(deactivated)).GetProperty("isActive").GetBoolean());

        var activeOnly = await owner.GetFromJsonAsync<JsonElement>($"{Route}?activeOnly=true");
        Assert.Equal(0, activeOnly.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Plans_InvalidBody_ReturnsValidationProblemWithStableCode()
    {
        var s = await SeedAsync();
        using var owner = Client(s.Owner, s.Tenant);

        var response = await owner.PostAsJsonAsync(Route, new
        {
            name = "Bad", price = -1, currency = "NGN",
            benefits = new[] { new { serviceOfferingId = s.Service, quantityPerCycle = 1 } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("retention.validation", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Plans_StarterTenant_IsDeniedOnEveryRoute()
    {
        var s = await SeedAsync(SubscriptionSku.Starter);
        using var owner = Client(s.Owner, s.Tenant);

        var create = await owner.PostAsJsonAsync(Route, PlanBody(s));
        var list = await owner.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Contains("entitlements.memberships", await create.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Plans_RequireAuth_AndRetentionPermissionForMutations()
    {
        var s = await SeedAsync();

        using var anon = Client(tenant: s.Tenant);
        Assert.Contains((await anon.GetAsync(Route)).StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        Assert.Contains((await anon.PostAsJsonAsync(Route, PlanBody(s))).StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });

        using var manager = Client(s.Manager, s.Tenant);
        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync(Route, PlanBody(s, "Manager plan"))).StatusCode);

        using var accountant = Client(s.Accountant, s.Tenant);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.PostAsJsonAsync(Route, PlanBody(s, "Nope"))).StatusCode);
    }

    [Fact]
    public async Task Plans_CrossTenant_AreDeniedAndInvisible()
    {
        var a = await SeedAsync();
        var b = await SeedAsync();
        using var ownerA = Client(a.Owner, a.Tenant);
        var id = (await ReadAsync(await ownerA.PostAsJsonAsync(Route, PlanBody(a)))).GetProperty("id").GetGuid();

        // Caller from tenant A pointing at tenant B's header → tenant middleware denies.
        using var crossHeader = Client(a.Owner, b.Tenant);
        Assert.Equal(HttpStatusCode.Forbidden, (await crossHeader.GetAsync(Route)).StatusCode);

        // Tenant B's own owner cannot see or touch A's plan.
        using var ownerB = Client(b.Owner, b.Tenant);
        var list = await ownerB.GetFromJsonAsync<JsonElement>(Route);
        Assert.Equal(0, list.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.GetAsync($"{Route}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.PostAsync($"{Route}/{id}/deactivate", null)).StatusCode);
    }
}
