namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Api.Middleware;
using Adeni.Domain.Identity;
using Adeni.Domain.Finance;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

public sealed class PaymentIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PaymentIntegrationTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty,
                    ["Redis:ConnectionString"] = string.Empty,
                    ["Auth0:Enabled"] = "false"
                });
            });
        });

    [Fact]
    public async Task Initialize_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var tenantId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            "/api/v1/finance/initialize",
            new
            {
                tenantId,
                bookingId = (Guid?)null,
                amount = 2500m,
                currency = "NGN",
                type = "link"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StubConfirm_OutsideDevelopment_Returns404()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IHostEnvironment>(new NonDevelopmentHostEnvironment());
            });
        });

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/finance/stub/confirm",
            new { reference = "stub_test_ref" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Ledger_CrossTenant_Returns403()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        const string ownerSub = "auth0|payment-owner-a";

        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty,
                    ["Redis:ConnectionString"] = string.Empty,
                    ["Auth0:Enabled"] = "false"
                });
            });
        });

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenantWithBusinessUser(db, tenantA, ownerSub);
            SeedTenantWithBusinessUser(db, tenantB, "auth0|payment-owner-b");
            db.PaymentIntents.Add(new PaymentIntentRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantB,
                Amount = 1000m,
                Currency = "NGN",
                Status = PaymentIntentStatus.Completed,
                ProviderReference = "ledger_cross_tenant",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            db.SaveChanges();
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantA.ToString());

        var response = await client.GetAsync($"/api/v1/finance/ledger?tenantId={tenantB}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Initialize_WithIdempotencyKey_ReturnsSamePaymentOnRetry()
    {
        const string customerSub = "auth0|payment-idem-customer";
        var tenantId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        const string idempotencyKey = "payment-idem-test-key";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Idem Tenant",
                Status = TenantStatus.Verified,
                CreatedAt = now,
                VerifiedAt = now,
            });
            db.BusinessProfiles.Add(new BusinessProfile
            {
                TenantId = tenantId,
                CategorySlug = "barbers",
                Phone = "+2348012345678",
                DepositPercent = 25,
                UpdatedAt = now,
            });
            db.ServiceOfferings.Add(new Adeni.Domain.Booking.ServiceOffering
            {
                Id = serviceId,
                TenantId = tenantId,
                Name = "Service",
                PriceAmount = 4000m,
                Currency = "NGN",
                DurationMinutes = 30,
                IsActive = true,
            });
            db.Customers.Add(new Customer { Id = customerId, Auth0Sub = customerSub });
            db.Bookings.Add(new Adeni.Domain.Booking.BookingRecord
            {
                Id = bookingId,
                TenantId = tenantId,
                CustomerId = customerId,
                ServiceOfferingId = serviceId,
                Status = Adeni.Domain.Booking.BookingStatus.Pending,
                StartAt = now.AddDays(1),
                GuestCount = 1,
            });
            db.SaveChanges();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevCustomerAuthMiddleware.DevAuth0SubHeader, customerSub);

        var body = new
        {
            tenantId,
            bookingId,
            currency = "NGN",
            type = "deposit",
        };

        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/finance/initialize")
        {
            Content = JsonContent.Create(body),
        };
        firstRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        using var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/finance/initialize")
        {
            Content = JsonContent.Create(body),
        };
        secondRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var firstResponse = await client.SendAsync(firstRequest);
        var secondResponse = await client.SendAsync(secondRequest);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<PaymentCreatedPayload>();
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<PaymentCreatedPayload>();
        Assert.NotNull(firstPayload);
        Assert.NotNull(secondPayload);
        Assert.Equal(firstPayload!.Id, secondPayload!.Id);
    }

    [Theory]
    [InlineData("business", false, HttpStatusCode.OK)]
    [InlineData("customer", false, HttpStatusCode.OK)]
    [InlineData("other-business", false, HttpStatusCode.Forbidden)]
    [InlineData("other-customer", false, HttpStatusCode.Forbidden)]
    [InlineData("anonymous", false, HttpStatusCode.Unauthorized)]
    [InlineData("business", true, HttpStatusCode.OK)]
    [InlineData("customer", true, HttpStatusCode.OK)]
    [InlineData("other-customer", true, HttpStatusCode.Forbidden)]
    public async Task PaymentReads_PreserveBusinessAndCustomerAccess(string caller, bool receipt, HttpStatusCode expected)
    {
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var member = $"auth0|payment-member-{paymentId:N}";
        var customer = $"auth0|payment-customer-{paymentId:N}";
        var otherMember = $"auth0|payment-other-member-{paymentId:N}";
        var otherCustomer = $"auth0|payment-other-customer-{paymentId:N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenantWithBusinessUser(db, tenantId, member);
            SeedTenantWithBusinessUser(db, Guid.NewGuid(), otherMember);
            db.Customers.AddRange(new Customer { Id = customerId, Auth0Sub = customer },
                new Customer { Id = Guid.NewGuid(), Auth0Sub = otherCustomer });
            db.Bookings.Add(new Adeni.Domain.Booking.BookingRecord
            {
                Id = bookingId, TenantId = tenantId, CustomerId = customerId,
                Status = Adeni.Domain.Booking.BookingStatus.Paid,
            });
            db.PaymentIntents.Add(new PaymentIntentRecord
            {
                Id = paymentId, TenantId = tenantId, BookingId = bookingId, Amount = 100m,
                Status = PaymentIntentStatus.Completed, ProviderReference = $"access-{paymentId:N}",
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        if (caller.Contains("business"))
        {
            client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, caller == "business" ? member : otherMember);
        }
        else if (caller.Contains("customer"))
        {
            client.DefaultRequestHeaders.Add(DevCustomerAuthMiddleware.DevAuth0SubHeader, caller == "customer" ? customer : otherCustomer);
        }

        var response = await client.GetAsync($"/api/v1/finance/{paymentId}" + (receipt ? "/receipt" : ""));
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.Forbidden)
        {
            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal("Cross-tenant access denied.", body.GetProperty("title").GetString());
        }
    }

    [Fact]
    public async Task Ledger_PreservesFinancePermissionRequirement()
    {
        var tenantId = Guid.NewGuid();
        var sub = $"auth0|practitioner-{tenantId:N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenantWithBusinessUser(db, tenantId, sub);
            db.BusinessUsers.Local.Single(x => x.TenantId == tenantId).Role = "practitioner";
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, sub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/finance/ledger?tenantId={tenantId}")).StatusCode);
    }

    [Fact]
    public async Task Ledger_Owner_PreservesEnvelopeAndLocationValidation()
    {
        var tenantId = Guid.NewGuid();
        var sub = $"auth0|ledger-owner-{tenantId:N}";
        var foreignLocation = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenantWithBusinessUser(db, tenantId, sub);
            db.BusinessLocations.Add(new BusinessLocation { Id = foreignLocation, TenantId = Guid.NewGuid() });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, sub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());
        var response = await client.GetAsync($"/api/v1/finance/ledger?tenantId={tenantId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(50, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
        var invalid = await client.GetAsync($"/api/v1/finance/ledger?tenantId={tenantId}&locationId={foreignLocation}");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("Unknown or inactive location for this business.", await invalid.Content.ReadAsStringAsync());
    }

    private sealed record PaymentCreatedPayload(Guid Id);

    private static void SeedTenantWithBusinessUser(AdeniDbContext db, Guid tenantId, string auth0Sub)
    {
        var now = DateTimeOffset.UtcNow;

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {tenantId:N}"[..20],
            Status = TenantStatus.Verified,
            CreatedAt = now,
            VerifiedAt = now
        });

        db.BusinessUsers.Add(new BusinessUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Auth0Sub = auth0Sub,
            Role = "owner",
            CreatedAt = now
        });

        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "barbers",
            Phone = "+2348012345678",
            UpdatedAt = now
        });
    }

    private sealed class NonDevelopmentHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Staging;

        public string ApplicationName { get; set; } = "Adeni.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
