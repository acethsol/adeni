namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Domain.Booking;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class BookingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BookingIntegrationTests(WebApplicationFactory<Program> factory) =>
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
    public async Task Public_services_slots_and_booking_flow_work()
    {
        var slug = $"book-{Guid.NewGuid():N}"[..20];
        var tenantId = Guid.Empty;
        var serviceId = Guid.Empty;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            tenantId = Guid.NewGuid();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Booking Integration Shop",
                Status = TenantStatus.Verified,
                CreatedAt = DateTimeOffset.UtcNow,
                VerifiedAt = DateTimeOffset.UtcNow
            });
            db.BusinessProfiles.Add(new BusinessProfile
            {
                TenantId = tenantId,
                CategorySlug = "barbers",
                Phone = "+2348011111111",
                Description = "Integration booking",
                UpdatedAt = DateTimeOffset.UtcNow
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Slug = slug,
                Name = "Lekki",
                MarketId = "lagos",
                AddressLine = "1 Booking Road",
                Area = "Lekki",
                IsPrimary = true,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            serviceId = Guid.NewGuid();
            db.ServiceOfferings.Add(new ServiceOffering
            {
                Id = serviceId,
                TenantId = tenantId,
                Name = "Haircut",
                PriceAmount = 4000m,
                Currency = "NGN",
                DurationMinutes = 30,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            db.WeeklyAvailabilities.Add(new WeeklyAvailability
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DayOfWeek = DayOfWeek.Monday,
                OpenTime = new TimeOnly(9, 0),
                CloseTime = new TimeOnly(17, 0)
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();

        var servicesResponse = await client.GetAsync($"/api/v1/businesses/{slug}/services");
        Assert.Equal(HttpStatusCode.OK, servicesResponse.StatusCode);

        var monday = NextMondayUtc();
        var from = monday.AddHours(-1);
        var to = monday.AddDays(1);
        var slotsResponse = await client.GetAsync(
            $"/api/v1/businesses/{slug}/slots?serviceId={serviceId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, slotsResponse.StatusCode);

        var slotsPayload = await slotsResponse.Content.ReadFromJsonAsync<SlotsPayload>();
        Assert.NotNull(slotsPayload);
        Assert.NotEmpty(slotsPayload!.Items);

        var bookingClient = _factory.CreateClient();
        bookingClient.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub", "auth0|booking-customer");

        var createResponse = await bookingClient.PostAsJsonAsync(
            "/api/v1/bookings",
            new
            {
                tenantId,
                serviceOfferingId = serviceId,
                startAt = slotsPayload.Items[0].StartAt,
                customerNotes = "Integration test"
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
    }

    private sealed record SlotsPayload(IReadOnlyList<SlotItem> Items);

    private sealed record SlotItem(DateTimeOffset StartAt, DateTimeOffset EndAt);

    private static DateTimeOffset NextMondayUtc()
    {
        var today = DateTimeOffset.UtcNow;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0)
        {
            daysUntilMonday = 7;
        }

        return today.Date.AddDays(daysUntilMonday).AddHours(10);
    }
}
