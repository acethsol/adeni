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

    [Fact]
    public async Task Tenant_quotes_list_filters_by_locationId()
    {
        const string ownerSub = "auth0|quote-loc-filter-owner";
        var tenantId = Guid.NewGuid();
        var locA = Guid.NewGuid();
        var locB = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var quoteA = Guid.NewGuid();
        var quoteB = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Quote Loc Filter",
                Status = TenantStatus.Verified,
                CreatedAt = createdAt,
                VerifiedAt = createdAt,
            });
            db.BusinessUsers.Add(new BusinessUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Auth0Sub = ownerSub,
                Role = "owner",
                CreatedAt = createdAt,
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = locA,
                TenantId = tenantId,
                Slug = $"quote-loc-a-{tenantId:N}"[..18],
                Name = "A",
                MarketId = "lagos",
                AddressLine = "1",
                Area = "A",
                IsPrimary = true,
                IsActive = true,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = locB,
                TenantId = tenantId,
                Slug = $"quote-loc-b-{tenantId:N}"[..18],
                Name = "B",
                MarketId = "lagos",
                AddressLine = "2",
                Area = "B",
                IsPrimary = false,
                IsActive = true,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            });
            db.Customers.Add(new Customer
            {
                Id = customerId,
                Auth0Sub = CustomerSub,
                Name = "Quote Customer",
                CreatedAt = createdAt,
            });
            db.QuoteRequests.Add(new QuoteRequestRecord
            {
                Id = quoteA,
                TenantId = tenantId,
                BusinessLocationId = locA,
                CustomerId = customerId,
                Description = "Branch A quote request description.",
                Status = QuoteRequestStatus.Submitted,
                CreatedAt = createdAt,
            });
            db.QuoteRequests.Add(new QuoteRequestRecord
            {
                Id = quoteB,
                TenantId = tenantId,
                BusinessLocationId = locB,
                CustomerId = customerId,
                Description = "Branch B quote request description.",
                Status = QuoteRequestStatus.Submitted,
                CreatedAt = createdAt,
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var defaultResponse = await client.GetAsync("/api/v1/tenant/quotes");
        var locAResponse = await client.GetAsync($"/api/v1/tenant/quotes?locationId={locA:D}");
        var locBResponse = await client.GetAsync($"/api/v1/tenant/quotes?locationId={locB:D}");

        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, locAResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, locBResponse.StatusCode);

        var defaultPayload = await defaultResponse.Content.ReadFromJsonAsync<TenantQuotesPayload>();
        var locAPayload = await locAResponse.Content.ReadFromJsonAsync<TenantQuotesPayload>();
        var locBPayload = await locBResponse.Content.ReadFromJsonAsync<TenantQuotesPayload>();

        Assert.NotNull(defaultPayload);
        Assert.NotNull(locAPayload);
        Assert.NotNull(locBPayload);
        Assert.Single(defaultPayload!.Items);
        Assert.Equal(quoteA, defaultPayload.Items[0].Id);
        Assert.Single(locAPayload!.Items);
        Assert.Equal(quoteA, locAPayload.Items[0].Id);
        Assert.Single(locBPayload!.Items);
        Assert.Equal(quoteB, locBPayload.Items[0].Id);
    }

    private sealed record QuotePayload(
        Guid Id,
        string Status,
        decimal? QuotedAmount,
        Guid? BookingId);

    private sealed record CustomerBookingsPayload(IReadOnlyList<CustomerBookingItem> Items);

    private sealed record CustomerBookingItem(Guid Id);

    private sealed record TenantQuotesPayload(IReadOnlyList<TenantQuoteItem> Items);

    private sealed record TenantQuoteItem(Guid Id);
}
