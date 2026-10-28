namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Retention;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class RetentionIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public RetentionIntegrationTests(WebApplicationFactory<Program> factory) =>
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
        Guid Tenant, string Slug, Guid Service, Guid Location, Guid TenantCustomer,
        string Owner, string Manager, string Customer);

    private async Task<Seeded> SeedAsync()
    {
        var tenant = Guid.NewGuid();
        var slug = $"ret-{Guid.NewGuid():N}"[..20];
        var location = Guid.NewGuid();
        var service = Guid.NewGuid();
        var platformCustomer = Guid.NewGuid();
        var tenantCustomer = Guid.NewGuid();
        var owner = $"owner-{tenant:N}";
        var manager = $"manager-{tenant:N}";
        var customer = $"customer-{tenant:N}";
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.Tenants.Add(new Tenant { Id = tenant, Name = "Retention Salon", Status = TenantStatus.Verified, CreatedAt = now, VerifiedAt = now });
        db.BusinessProfiles.Add(new BusinessProfile { TenantId = tenant, CategorySlug = "barbers", Phone = "+2348011111111", Description = "Retention", UpdatedAt = now });
        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = location, TenantId = tenant, Slug = slug, Name = "Lekki", MarketId = "lagos", AddressLine = "1 Road",
            Area = "Lekki", IsPrimary = true, IsActive = true, CreatedAt = now, UpdatedAt = now,
        });
        db.ServiceOfferings.Add(new ServiceOffering
        {
            Id = service, TenantId = tenant, Name = "Haircut", PriceAmount = 4000m, Currency = "NGN",
            DurationMinutes = 30, IsActive = true, CreatedAt = now, UpdatedAt = now,
        });
        db.WeeklyAvailabilities.Add(new WeeklyAvailability
        {
            Id = Guid.NewGuid(), TenantId = tenant, BusinessLocationId = location, DayOfWeek = DayOfWeek.Monday,
            OpenTime = new TimeOnly(9, 0), CloseTime = new TimeOnly(17, 0),
        });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Auth0Sub = owner, Role = "owner" });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Auth0Sub = manager, Role = "manager" });
        db.Customers.Add(new Customer { Id = platformCustomer, Auth0Sub = customer, Name = "Ada" });
        db.TenantCustomers.Add(new TenantCustomer
        {
            Id = tenantCustomer, TenantId = tenant, CustomerId = platformCustomer, DisplayName = "Ada",
            CreatedAt = now, UpdatedAt = now, LastSeenAt = now,
        });
        await db.SaveChangesAsync();
        return new Seeded(tenant, slug, service, location, tenantCustomer, owner, manager, customer);
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

    private static HttpRequestMessage Post(string url, object? body, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return request;
    }

    private async Task<Guid> CreateDefinitionAsync(HttpClient owner, Seeded s)
    {
        var response = await owner.PostAsJsonAsync("/api/v1/tenant/retention/packages", new
        {
            name = "Cut x3",
            listPrice = 10000,
            currency = "NGN",
            benefits = new[] { new { serviceOfferingId = s.Service, quantityGranted = 3 } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> SellAsync(HttpClient actor, Seeded s, Guid definition, string key)
    {
        var response = await actor.SendAsync(Post(
            $"/api/v1/tenant/retention/customers/{s.TenantCustomer}/packages/sell",
            new { packageDefinitionId = definition, businessLocationId = s.Location, paymentMethod = "till" }, key));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<(int Reserved, int Used, int Remaining)> BalanceAsync(HttpClient owner, Seeded s)
    {
        var list = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/tenant/retention/customers/{s.TenantCustomer}/packages");
        var balance = list.GetProperty("items")[0].GetProperty("balances")[0];
        return (balance.GetProperty("reserved").GetInt32(), balance.GetProperty("used").GetInt32(), balance.GetProperty("remaining").GetInt32());
    }

    private static DateTimeOffset NextMondayUtc()
    {
        var today = DateTimeOffset.UtcNow;
        var days = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        return today.Date.AddDays(days == 0 ? 7 : days).AddHours(10);
    }

    private async Task<IReadOnlyList<DateTimeOffset>> SlotsAsync(Seeded s)
    {
        using var anon = Client();
        var monday = NextMondayUtc();
        var response = await anon.GetAsync(
            $"/api/v1/businesses/{s.Slug}/slots?serviceId={s.Service}&from={Uri.EscapeDataString(monday.AddHours(-1).ToString("O"))}&to={Uri.EscapeDataString(monday.AddDays(1).ToString("O"))}");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("startAt").GetDateTimeOffset()).ToList();
    }

    private static async Task<HttpResponseMessage> BookWithCreditAsync(
        HttpClient customer, Seeded s, DateTimeOffset start, Guid package, Guid balance, string key) =>
        await customer.SendAsync(Post("/api/v1/bookings", new
        {
            tenantId = s.Tenant,
            serviceOfferingId = s.Service,
            startAt = start,
            appliedBenefits = new[] { new { customerPackageId = package, benefitBalanceId = balance, quantity = 1 } },
        }, key));

    [Fact]
    public async Task Sell_ReplayWithSameKey_ReturnsSamePackage_AndMissingKeyIsRejected()
    {
        var s = await SeedAsync();
        using var owner = Client(s.Owner, s.Tenant);
        var definition = await CreateDefinitionAsync(owner, s);

        var first = await SellAsync(owner, s, definition, "sell-key-1");
        var second = await SellAsync(owner, s, definition, "sell-key-1");
        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());

        var missing = await owner.SendAsync(Post(
            $"/api/v1/tenant/retention/customers/{s.TenantCustomer}/packages/sell",
            new { packageDefinitionId = definition, businessLocationId = s.Location, paymentMethod = "till" }, null));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var conflict = await owner.SendAsync(Post(
            $"/api/v1/tenant/retention/customers/{s.TenantCustomer}/packages/sell",
            new { packageDefinitionId = definition, businessLocationId = s.Location, paymentMethod = "transfer" }, "sell-key-1"));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Retention_Endpoints_RequireAuth_AndDenyCrossTenantCallers()
    {
        var a = await SeedAsync();
        var b = await SeedAsync();

        using var anon = Client(tenant: a.Tenant);
        Assert.Contains((await anon.GetAsync("/api/v1/tenant/retention/packages")).StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        Assert.Contains((await anon.SendAsync(Post(
            $"/api/v1/tenant/retention/customers/{a.TenantCustomer}/packages/sell", new { }, "k"))).StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });

        using var crossTenant = Client(a.Owner, b.Tenant);
        Assert.Equal(HttpStatusCode.Forbidden, (await crossTenant.GetAsync("/api/v1/tenant/retention/packages")).StatusCode);
    }

    [Fact]
    public async Task Void_IsOwnerOnly()
    {
        var s = await SeedAsync();
        using var owner = Client(s.Owner, s.Tenant);
        using var manager = Client(s.Manager, s.Tenant);
        var definition = await CreateDefinitionAsync(owner, s);
        var package = (await SellAsync(manager, s, definition, "sell-1")).GetProperty("id").GetGuid();

        var denied = await manager.SendAsync(Post($"/api/v1/tenant/retention/customer-packages/{package}/void", new { reason = "x" }, "v1"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var allowed = await owner.SendAsync(Post($"/api/v1/tenant/retention/customer-packages/{package}/void", new { reason = "refund" }, "v2"));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal("voided", (await allowed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Booking_WithAppliedCredit_ReservesOnCreate_ReleasesOnCancel_AndRedeemsOnComplete()
    {
        var s = await SeedAsync();
        using var owner = Client(s.Owner, s.Tenant);
        using var customer = Client(s.Customer);
        var definition = await CreateDefinitionAsync(owner, s);
        var sold = await SellAsync(owner, s, definition, "sell-1");
        var package = sold.GetProperty("id").GetGuid();
        var balance = sold.GetProperty("balances")[0].GetProperty("id").GetGuid();
        var slots = await SlotsAsync(s);
        Assert.True(slots.Count >= 2);

        // Reserve on create.
        var created = await BookWithCreditAsync(customer, s, slots[0], package, balance, "book-1");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var bookingId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal((1, 0, 2), await BalanceAsync(owner, s));

        // Release on cancel.
        var cancelled = await customer.PostAsync($"/api/v1/bookings/{bookingId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal((0, 0, 3), await BalanceAsync(owner, s));

        // Finalize on completion.
        var second = await BookWithCreditAsync(customer, s, slots[1], package, balance, "book-2");
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/v1/tenant/bookings/{secondId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/v1/tenant/bookings/{secondId}/check-in", null)).StatusCode);
        var completed = await owner.PostAsync($"/api/v1/tenant/bookings/{secondId}/complete", null);
        Assert.True(completed.IsSuccessStatusCode, await completed.Content.ReadAsStringAsync());
        Assert.Equal((0, 1, 2), await BalanceAsync(owner, s));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var kinds = await db.Set<BenefitTransaction>().AsNoTracking()
            .Where(x => x.TenantId == s.Tenant).OrderBy(x => x.OccurredAt).ThenBy(x => x.Sequence)
            .Select(x => x.Kind).ToListAsync();
        Assert.Contains(BenefitTransactionKind.Release, kinds);
        Assert.Contains(BenefitTransactionKind.Redeem, kinds);
    }

    [Fact]
    public async Task Booking_ExceedingRemainingCredit_IsRejectedWithStableCode()
    {
        var s = await SeedAsync();
        using var owner = Client(s.Owner, s.Tenant);
        using var customer = Client(s.Customer);
        var createdDefinition = await owner.PostAsJsonAsync("/api/v1/tenant/retention/packages", new
        {
            name = "Single",
            listPrice = 4000,
            currency = "NGN",
            benefits = new[] { new { serviceOfferingId = s.Service, quantityGranted = 1 } },
        });
        var definition = (await createdDefinition.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var sold = await SellAsync(owner, s, definition, "sell-1");
        var package = sold.GetProperty("id").GetGuid();
        var balance = sold.GetProperty("balances")[0].GetProperty("id").GetGuid();
        var slots = await SlotsAsync(s);

        Assert.Equal(HttpStatusCode.Created, (await BookWithCreditAsync(customer, s, slots[0], package, balance, "b1")).StatusCode);
        var over = await BookWithCreditAsync(customer, s, slots[1], package, balance, "b2");

        Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
        var problem = await over.Content.ReadAsStringAsync();
        Assert.Contains("retention.benefit_insufficient", problem);
    }
}
