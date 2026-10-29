namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Adeni.Domain.Identity;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class LoyaltyIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Loyalty = "/api/v1/tenant/retention/loyalty";
    private readonly WebApplicationFactory<Program> factory;

    public LoyaltyIntegrationTests(WebApplicationFactory<Program> factory) =>
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

    private sealed record Seeded(Guid Tenant, Guid Customer, string Owner, string Manager, string Receptionist, string Accountant);

    private async Task<Seeded> SeedAsync(SubscriptionSku sku = SubscriptionSku.Starter)
    {
        var tenant = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var owner = $"owner-{tenant:N}";
        var manager = $"manager-{tenant:N}";
        var receptionist = $"recep-{tenant:N}";
        var accountant = $"accountant-{tenant:N}";
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = tenant, Name = "Loyalty Salon", Status = TenantStatus.Verified,
            SubscriptionSku = sku, BillingStatus = TenantBillingStatus.Active, CreatedAt = now, VerifiedAt = now,
        });
        db.Add(new TenantCustomer
        {
            Id = customer, TenantId = tenant, CustomerId = Guid.NewGuid(), DisplayName = "Ada",
            CreatedAt = now, UpdatedAt = now, LastSeenAt = now,
        });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Auth0Sub = owner, Role = "owner" });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Auth0Sub = manager, Role = "manager" });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Auth0Sub = receptionist, Role = "receptionist" });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Auth0Sub = accountant, Role = "accountant" });
        await db.SaveChangesAsync();
        return new Seeded(tenant, customer, owner, manager, receptionist, accountant);
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

    private static object ProgramBody(bool enabled = true) => new
    {
        isEnabled = enabled, currency = "NGN", earnPoints = 1, earnPerAmount = 100, redeemPoints = 100,
        redeemValue = 500, minRedeemPoints = 100, pointsExpiryMonths = 12,
    };

    private static HttpRequestMessage AdjustRequest(Seeded s, int points, string? key, string reason = "Goodwill")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{Loyalty}/customers/{s.Customer}/adjust")
        {
            Content = JsonContent.Create(new { points, reason }),
        };
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return request;
    }

    [Fact]
    public async Task Program_OwnerSaves_ManagerAndReceptionistRead_NonOwnersCannotWrite()
    {
        var s = await SeedAsync();
        using var owner = Client(s.Owner, s.Tenant);
        using var manager = Client(s.Manager, s.Tenant);
        using var receptionist = Client(s.Receptionist, s.Tenant);

        var initial = await ReadAsync(await owner.GetAsync($"{Loyalty}/program"));
        Assert.False(initial.GetProperty("configured").GetBoolean());

        var saved = await owner.PutAsJsonAsync($"{Loyalty}/program", ProgramBody());
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var body = await ReadAsync(saved);
        Assert.True(body.GetProperty("configured").GetBoolean());
        Assert.True(body.GetProperty("isEnabled").GetBoolean());

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync($"{Loyalty}/program", ProgramBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await receptionist.PutAsJsonAsync($"{Loyalty}/program", ProgramBody())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"{Loyalty}/program")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await receptionist.GetAsync($"{Loyalty}/program")).StatusCode);
    }

    [Fact]
    public async Task Program_InvalidBody_Returns400WithStableCode()
    {
        var s = await SeedAsync();
        using var owner = Client(s.Owner, s.Tenant);

        var response = await owner.PutAsJsonAsync($"{Loyalty}/program", new
        {
            isEnabled = true, currency = "NGN", earnPoints = 0, earnPerAmount = 100, redeemPoints = 100,
            redeemValue = 500, minRedeemPoints = 100,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("retention.validation", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Adjust_ManagerPosts_ThenAccountAndLedgerReflectIt_AndReplayIsIdempotent()
    {
        var s = await SeedAsync(SubscriptionSku.Professional);
        using var owner = Client(s.Owner, s.Tenant);
        using var manager = Client(s.Manager, s.Tenant);
        await owner.PutAsJsonAsync($"{Loyalty}/program", ProgramBody());

        var first = await manager.SendAsync(AdjustRequest(s, 120, "adj-1"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await ReadAsync(first);
        Assert.Equal(120, firstBody.GetProperty("account").GetProperty("pointsBalance").GetInt32());
        var txnId = firstBody.GetProperty("transaction").GetProperty("id").GetGuid();

        var replay = await ReadAsync(await manager.SendAsync(AdjustRequest(s, 120, "adj-1")));
        Assert.Equal(txnId, replay.GetProperty("transaction").GetProperty("id").GetGuid());

        var account = await manager.GetFromJsonAsync<JsonElement>($"{Loyalty}/customers/{s.Customer}/account");
        Assert.Equal(120, account.GetProperty("pointsBalance").GetInt32());

        var ledger = await manager.GetFromJsonAsync<JsonElement>($"{Loyalty}/customers/{s.Customer}/transactions?limit=10");
        Assert.Equal(1, ledger.GetProperty("items").GetArrayLength());
        Assert.Equal("adjust", ledger.GetProperty("items")[0].GetProperty("kind").GetString());

        var conflict = await manager.SendAsync(AdjustRequest(s, 121, "adj-1"));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("retention.idempotency_conflict", await conflict.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Adjust_Rules_KeyRequired_BelowZero_NoProgram_AndRoleDenied()
    {
        var s = await SeedAsync();
        using var owner = Client(s.Owner, s.Tenant);
        using var receptionist = Client(s.Receptionist, s.Tenant);

        var noProgram = await owner.SendAsync(AdjustRequest(s, 5, "k0"));
        Assert.Equal(HttpStatusCode.Conflict, noProgram.StatusCode);
        Assert.Contains("retention.loyalty_program_not_configured", await noProgram.Content.ReadAsStringAsync());

        await owner.PutAsJsonAsync($"{Loyalty}/program", ProgramBody());

        var missing = await owner.SendAsync(AdjustRequest(s, 5, null));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("retention.idempotency_key_required", await missing.Content.ReadAsStringAsync());

        var below = await owner.SendAsync(AdjustRequest(s, -5, "k1"));
        Assert.Equal(HttpStatusCode.Conflict, below.StatusCode);
        Assert.Contains("retention.loyalty_insufficient_points", await below.Content.ReadAsStringAsync());

        var denied = await receptionist.SendAsync(AdjustRequest(s, 5, "k2"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("retention.owner_required", await denied.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Loyalty_RequireAuth_AndRetentionPermission()
    {
        var s = await SeedAsync();

        using var anon = Client(tenant: s.Tenant);
        Assert.Contains(
            (await anon.GetAsync($"{Loyalty}/program")).StatusCode,
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        Assert.Contains(
            (await anon.SendAsync(AdjustRequest(s, 5, "k"))).StatusCode,
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });

        using var accountant = Client(s.Accountant, s.Tenant);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.GetAsync($"{Loyalty}/program")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.SendAsync(AdjustRequest(s, 5, "k"))).StatusCode);
    }

    [Fact]
    public async Task Loyalty_CrossTenant_IsDeniedAndInvisible()
    {
        var a = await SeedAsync();
        var b = await SeedAsync();
        using var ownerA = Client(a.Owner, a.Tenant);
        await ownerA.PutAsJsonAsync($"{Loyalty}/program", ProgramBody());
        await ownerA.SendAsync(AdjustRequest(a, 50, "k"));

        // Tenant A's user claiming tenant B via the header is rejected by TenantAccessMiddleware.
        using var crossHeader = Client(a.Owner, b.Tenant);
        Assert.Equal(HttpStatusCode.Forbidden, (await crossHeader.GetAsync($"{Loyalty}/program")).StatusCode);

        using var ownerB = Client(b.Owner, b.Tenant);
        var bProgram = await ReadAsync(await ownerB.GetAsync($"{Loyalty}/program"));
        Assert.False(bProgram.GetProperty("configured").GetBoolean());
        // Tenant B cannot read or adjust tenant A's customer.
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.GetAsync($"{Loyalty}/customers/{a.Customer}/account")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.GetAsync($"{Loyalty}/customers/{a.Customer}/transactions")).StatusCode);
        await ownerB.PutAsJsonAsync($"{Loyalty}/program", ProgramBody());
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.SendAsync(AdjustRequest(a, 5, "x"))).StatusCode);
    }
}
