namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Api.Middleware;
using Adeni.Domain.Identity;
using Adeni.Domain.Payments;
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
            "/api/v1/payments/initialize",
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
            "/api/v1/payments/stub/confirm",
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

        var response = await client.GetAsync($"/api/v1/payments/ledger?tenantId={tenantB}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Initialize_WithIdempotencyKey_ReturnsSamePaymentOnRetry()
    {
        const string customerSub = "auth0|payment-idem-customer";
        var tenantId = Guid.NewGuid();
        const string idempotencyKey = "payment-idem-test-key";

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevCustomerAuthMiddleware.DevAuth0SubHeader, customerSub);

        var body = new
        {
            tenantId,
            bookingId = (Guid?)null,
            amount = 1500m,
            currency = "NGN",
            type = "link"
        };

        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/initialize")
        {
            Content = JsonContent.Create(body),
        };
        firstRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        using var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/initialize")
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
