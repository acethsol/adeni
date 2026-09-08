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

public sealed class QuoteIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CustomerSub = "auth0|quote-e2e-customer";
    private const string OwnerSub = "auth0|quote-e2e-owner";

    private readonly WebApplicationFactory<Program> _factory;

    public QuoteIntegrationTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty,
                    ["Redis:ConnectionString"] = string.Empty
                });
            });
        });

    [Fact]
    public async Task Quote_request_offer_accept_creates_booking()
    {
        var slug = $"quote-{Guid.NewGuid():N}"[..18];
        var tenantId = Guid.Empty;
        var serviceId = Guid.Empty;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            tenantId = Guid.NewGuid();
            serviceId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;

            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Quote E2E Plumbing",
                Status = TenantStatus.Verified,
                CreatedAt = now,
                VerifiedAt = now,
            });
            db.BusinessUsers.Add(new BusinessUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Auth0Sub = OwnerSub,
                Role = "owner",
                CreatedAt = now,
            });
            db.BusinessProfiles.Add(new BusinessProfile
            {
                TenantId = tenantId,
                CategorySlug = "plumbers",
                BusinessType = BusinessType.QuoteRequest,
                Phone = "+2348011111111",
                Description = "Quote integration",
                UpdatedAt = now,
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Slug = slug,
                Name = "Lekki",
                MarketId = "lagos",
                AddressLine = "1 Quote Road",
                Area = "Lekki",
                IsPrimary = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.ServiceOfferings.Add(new ServiceOffering
            {
                Id = serviceId,
                TenantId = tenantId,
                Name = "Pipe repair",
                PriceAmount = 0m,
                Currency = "NGN",
                DurationMinutes = 60,
                PricingType = PricingType.QuoteRequest,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.TenantVerificationBadges.Add(new TenantVerificationBadge
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BadgeType = VerificationBadgeType.License,
                Status = VerificationBadgeStatus.Granted,
                GrantedAt = now,
                RequestedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var customerClient = _factory.CreateClient();
        customerClient.DefaultRequestHeaders.Add(DevCustomerAuthMiddleware.DevAuth0SubHeader, CustomerSub);

        var createResponse = await customerClient.PostAsJsonAsync(
            $"/api/v1/businesses/{slug}/quote-requests",
            new
            {
                description = "Kitchen sink is leaking under the cabinet.",
                serviceAddress = "12 Admiralty Way, Lekki",
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<QuotePayload>();
        Assert.NotNull(created);
        Assert.Equal("submitted", created!.Status);

        var businessClient = _factory.CreateClient();
        businessClient.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, OwnerSub);
        businessClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var startAt = DateTimeOffset.UtcNow.AddDays(3);
        var endAt = startAt.AddMinutes(60);
        var offerResponse = await businessClient.PostAsJsonAsync(
            $"/api/v1/tenant/quotes/{created.Id}/offer",
            new
            {
                amount = 25000m,
                currency = "NGN",
                notes = "Includes parts and labour",
                serviceOfferingId = serviceId,
                proposedStartAt = startAt,
                proposedEndAt = endAt,
            });
        Assert.Equal(HttpStatusCode.OK, offerResponse.StatusCode);
        var offered = await offerResponse.Content.ReadFromJsonAsync<QuotePayload>();
        Assert.NotNull(offered);
        Assert.Equal("quoted", offered!.Status);
        Assert.Equal(25000m, offered.QuotedAmount);

        var acceptResponse = await customerClient.PostAsync(
            $"/api/v1/quotes/{created.Id}/accept",
            null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        var accepted = await acceptResponse.Content.ReadFromJsonAsync<QuotePayload>();
        Assert.NotNull(accepted);
        Assert.Equal("accepted", accepted!.Status);
        Assert.NotNull(accepted.BookingId);

        var bookingsResponse = await customerClient.GetAsync("/api/v1/bookings");
        Assert.Equal(HttpStatusCode.OK, bookingsResponse.StatusCode);
        var bookings = await bookingsResponse.Content.ReadFromJsonAsync<CustomerBookingsPayload>();
        Assert.NotNull(bookings);
        Assert.Contains(bookings!.Items, item => item.Id == accepted.BookingId);
    }

    private sealed record QuotePayload(
        Guid Id,
        string Status,
        decimal? QuotedAmount,
        Guid? BookingId);

    private sealed record CustomerBookingsPayload(IReadOnlyList<CustomerBookingItem> Items);

    private sealed record CustomerBookingItem(Guid Id);
}
