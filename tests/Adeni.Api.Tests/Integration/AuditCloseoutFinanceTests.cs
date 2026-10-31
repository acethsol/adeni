namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Api.Middleware;
using Adeni.Domain.Booking;
using Adeni.Domain.Finance;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class AuditCloseoutFinanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuditCloseoutFinanceTests(WebApplicationFactory<Program> factory) =>
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
    public async Task InitializeDeposit_OtherCustomersBooking_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var ownerCustomerId = Guid.NewGuid();
        const string ownerSub = "auth0|deposit-owner";
        const string otherSub = "auth0|deposit-other";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedDepositBooking(db, tenantId, bookingId, ownerCustomerId, ownerSub, depositPercent: 20, servicePrice: 10_000m);
            db.Customers.Add(new Customer { Id = Guid.NewGuid(), Auth0Sub = otherSub });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevCustomerAuthMiddleware.DevAuth0SubHeader, otherSub);

        var response = await client.PostAsJsonAsync(
            "/api/v1/finance/initialize",
            new { tenantId, bookingId, currency = "NGN", type = "deposit", amount = 100m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InitializeDeposit_ClientAmountIgnored_UsesRequiredDeposit()
    {
        var tenantId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        const string customerSub = "auth0|deposit-amount";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedDepositBooking(db, tenantId, bookingId, customerId, customerSub, depositPercent: 20, servicePrice: 10_000m);
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevCustomerAuthMiddleware.DevAuth0SubHeader, customerSub);

        var response = await client.PostAsJsonAsync(
            "/api/v1/finance/initialize",
            new { tenantId, bookingId, currency = "NGN", type = "deposit", amount = 1m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<PaymentCreatedPayload>();
        Assert.NotNull(payload);
        Assert.Equal(2000m, payload!.Amount);
    }

    [Fact]
    public async Task PaymentGet_PractitionerWithoutFinance_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var practitionerSub = $"auth0|practitioner-pay-{tenantId:N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenantWithBusinessUser(db, tenantId, practitionerSub, "practitioner");
            db.Customers.Add(new Customer { Id = customerId, Auth0Sub = "auth0|cust" });
            db.Bookings.Add(new BookingRecord
            {
                Id = bookingId,
                TenantId = tenantId,
                CustomerId = customerId,
                Status = BookingStatus.Pending,
            });
            db.PaymentIntents.Add(new PaymentIntentRecord
            {
                Id = paymentId,
                TenantId = tenantId,
                BookingId = bookingId,
                Amount = 100m,
                Currency = "NGN",
                Status = PaymentIntentStatus.Completed,
                ProviderReference = "practitioner-read",
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, practitionerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var response = await client.GetAsync($"/api/v1/finance/{paymentId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record PaymentCreatedPayload(Guid Id, decimal Amount);

    private static void SeedDepositBooking(
        AdeniDbContext db,
        Guid tenantId,
        Guid bookingId,
        Guid customerId,
        string customerSub,
        int depositPercent,
        decimal servicePrice)
    {
        var now = DateTimeOffset.UtcNow;
        var serviceId = Guid.NewGuid();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Deposit Tenant",
            Status = TenantStatus.Verified,
            CreatedAt = now,
            VerifiedAt = now,
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "barbers",
            Phone = "+2348012345678",
            DepositPercent = depositPercent,
            UpdatedAt = now,
        });
        db.ServiceOfferings.Add(new ServiceOffering
        {
            Id = serviceId,
            TenantId = tenantId,
            Name = "Cut",
            PriceAmount = servicePrice,
            Currency = "NGN",
            DurationMinutes = 30,
            IsActive = true,
        });
        db.Customers.Add(new Customer { Id = customerId, Auth0Sub = customerSub });
        db.Bookings.Add(new BookingRecord
        {
            Id = bookingId,
            TenantId = tenantId,
            CustomerId = customerId,
            ServiceOfferingId = serviceId,
            Status = BookingStatus.Pending,
            StartAt = now.AddDays(1),
            GuestCount = 1,
        });
    }

    private static void SeedTenantWithBusinessUser(
        AdeniDbContext db,
        Guid tenantId,
        string auth0Sub,
        string role)
    {
        var now = DateTimeOffset.UtcNow;
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {tenantId:N}"[..20],
            Status = TenantStatus.Verified,
            CreatedAt = now,
            VerifiedAt = now,
        });
        db.BusinessUsers.Add(new BusinessUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Auth0Sub = auth0Sub,
            Role = role,
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
}
