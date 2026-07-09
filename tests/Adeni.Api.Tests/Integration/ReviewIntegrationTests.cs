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

public sealed class ReviewIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CustomerSub = "auth0|review-e2e-customer";

    private readonly WebApplicationFactory<Program> _factory;

    public ReviewIntegrationTests(WebApplicationFactory<Program> factory) =>
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
    public async Task Completed_booking_review_flow_updates_public_ratings()
    {
        var slug = $"review-{Guid.NewGuid():N}"[..18];
        var tenantId = Guid.Empty;
        var serviceId = Guid.Empty;
        var bookingId = Guid.Empty;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            tenantId = Guid.NewGuid();
            bookingId = Guid.NewGuid();
            serviceId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var startAt = now.AddDays(-2);
            var endAt = startAt.AddMinutes(30);

            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Review E2E Salon",
                Status = TenantStatus.Verified,
                CreatedAt = now,
                VerifiedAt = now,
            });
            db.BusinessProfiles.Add(new BusinessProfile
            {
                TenantId = tenantId,
                CategorySlug = "hair-salons",
                Phone = "+2348011111111",
                Description = "Review integration",
                UpdatedAt = now,
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Slug = slug,
                Name = "Lekki",
                MarketId = "lagos",
                AddressLine = "1 Review Road",
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
                Name = "Styling",
                PriceAmount = 5000m,
                Currency = "NGN",
                DurationMinutes = 30,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.Customers.Add(new Customer
            {
                Id = customerId,
                Auth0Sub = CustomerSub,
                Name = "E2E Customer",
                CreatedAt = now,
            });
            db.Bookings.Add(new BookingRecord
            {
                Id = bookingId,
                TenantId = tenantId,
                ServiceOfferingId = serviceId,
                CustomerId = customerId,
                StartAt = startAt,
                EndAt = endAt,
                Status = BookingStatus.Confirmed,
                CreatedAt = startAt,
                UpdatedAt = startAt,
            });
            await db.SaveChangesAsync();
        }

        var customerClient = _factory.CreateClient();
        customerClient.DefaultRequestHeaders.Add(DevCustomerAuthMiddleware.DevAuth0SubHeader, CustomerSub);

        var listBefore = await customerClient.GetAsync("/api/v1/bookings");
        Assert.Equal(HttpStatusCode.OK, listBefore.StatusCode);
        var bookingsBefore = await listBefore.Content.ReadFromJsonAsync<CustomerBookingsPayload>();
        Assert.NotNull(bookingsBefore);
        var bookingItem = bookingsBefore!.Items.Single(x => x.Id == bookingId);
        Assert.True(bookingItem.CanReview);
        Assert.False(bookingItem.HasReview);

        var reviewResponse = await customerClient.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/review",
            new { rating = 5, comment = "Great visit" });
        Assert.Equal(HttpStatusCode.Created, reviewResponse.StatusCode);

        var publicClient = _factory.CreateClient();
        var reviewsResponse = await publicClient.GetAsync($"/api/v1/businesses/{slug}/reviews");
        Assert.Equal(HttpStatusCode.OK, reviewsResponse.StatusCode);
        var reviewsPayload = await reviewsResponse.Content.ReadFromJsonAsync<ReviewsPayload>();
        Assert.NotNull(reviewsPayload);
        Assert.Equal(1, reviewsPayload!.TotalCount);
        Assert.Equal(5, reviewsPayload.Items[0].Rating);
        Assert.Equal("Great visit", reviewsPayload.Items[0].Comment);

        var profileResponse = await publicClient.GetAsync($"/api/v1/businesses/{slug}");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        var profile = await profileResponse.Content.ReadFromJsonAsync<ProfilePayload>();
        Assert.NotNull(profile);
        Assert.Equal(5.0, profile!.RatingAvg);
        Assert.Equal(1, profile.ReviewCount);

        var duplicateReview = await customerClient.PostAsJsonAsync(
            $"/api/v1/bookings/{bookingId}/review",
            new { rating = 4 });
        Assert.Equal(HttpStatusCode.Conflict, duplicateReview.StatusCode);
    }

    private sealed record CustomerBookingsPayload(IReadOnlyList<CustomerBookingItem> Items);

    private sealed record CustomerBookingItem(
        Guid Id,
        bool CanReview,
        bool HasReview);

    private sealed record ReviewsPayload(
        IReadOnlyList<ReviewItem> Items,
        int TotalCount);

    private sealed record ReviewItem(byte Rating, string Comment);

    private sealed record ProfilePayload(double? RatingAvg, int ReviewCount);
}
