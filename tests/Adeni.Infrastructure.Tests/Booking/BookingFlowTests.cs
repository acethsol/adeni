namespace Adeni.Infrastructure.Tests.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Caching;
using Adeni.Application.Markets;
using Adeni.Application.Reviews;
using Adeni.Domain.Booking;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class BookingFlowTests
{
    private const string TestTimeZoneId = "UTC";

    [Fact]
    public async Task Create_booking_succeeds_for_available_slot()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);

        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var availability = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var service = await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Fade", "Skin fade", 5000m, "NGN", 30),
            CancellationToken.None);

        await availability.ReplaceWeeklyRulesAsync(
            tenantId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))],
            CancellationToken.None);

        var slotStart = NextMondayAt(new TimeOnly(10, 0));
        var created = await bookings.CreateAsync(
            "auth0|customer-1",
            new CreateBookingRequest(tenantId, service.Value!.Id, slotStart, "First visit"),
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.Equal(BookingStatus.Pending, created.Value!.Status);
        Assert.Equal("Fade", created.Value.ServiceName);
    }

    [Fact]
    public async Task Create_booking_rejects_conflicting_slot()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);

        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var availability = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var service = await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Fade", null, 5000m, "NGN", 30),
            CancellationToken.None);

        await availability.ReplaceWeeklyRulesAsync(
            tenantId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))],
            CancellationToken.None);

        var slotStart = NextMondayAt(new TimeOnly(10, 0));
        var first = await bookings.CreateAsync(
            "auth0|customer-1",
            new CreateBookingRequest(tenantId, service.Value!.Id, slotStart, null),
            CancellationToken.None);
        var second = await bookings.CreateAsync(
            "auth0|customer-2",
            new CreateBookingRequest(tenantId, service.Value.Id, slotStart, null),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal("conflict", second.Error.Code);
    }

    [Fact]
    public async Task Business_can_accept_pending_booking()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);

        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var availability = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var service = await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Fade", null, 5000m, "NGN", 30),
            CancellationToken.None);

        await availability.ReplaceWeeklyRulesAsync(
            tenantId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))],
            CancellationToken.None);

        var slotStart = NextMondayAt(new TimeOnly(11, 0));
        var created = await bookings.CreateAsync(
            "auth0|customer-1",
            new CreateBookingRequest(tenantId, service.Value!.Id, slotStart, null),
            CancellationToken.None);

        var accepted = await bookings.AcceptAsync(tenantId, created.Value!.Id, CancellationToken.None);

        Assert.True(accepted.IsSuccess);
        Assert.Equal(BookingStatus.Confirmed, accepted.Value!.Status);
    }

    [Fact]
    public async Task Available_slots_exclude_booked_times()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);

        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var availability = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var service = await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Fade", null, 5000m, "NGN", 30),
            CancellationToken.None);

        await availability.ReplaceWeeklyRulesAsync(
            tenantId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0))],
            CancellationToken.None);

        var slotStart = NextMondayAt(new TimeOnly(10, 0));
        await bookings.CreateAsync(
            "auth0|customer-1",
            new CreateBookingRequest(tenantId, service.Value!.Id, slotStart, null),
            CancellationToken.None);

        var rangeStart = slotStart.AddHours(-1);
        var rangeEnd = slotStart.AddHours(3);
        var slots = await availability.GetAvailableSlotsAsync(
            tenantId,
            service.Value.Id,
            rangeStart,
            rangeEnd,
            CancellationToken.None);

        Assert.True(slots.IsSuccess);
        Assert.DoesNotContain(slots.Value!, s => s.StartAt == slotStart);
    }

    [Fact]
    public async Task Customer_can_list_their_bookings()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);

        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var availability = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var service = await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Fade", null, 5000m, "NGN", 30),
            CancellationToken.None);

        await availability.ReplaceWeeklyRulesAsync(
            tenantId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))],
            CancellationToken.None);

        const string customerSub = "auth0|customer-list";
        var slotStart = NextMondayAt(new TimeOnly(10, 0));
        var created = await bookings.CreateAsync(
            customerSub,
            new CreateBookingRequest(tenantId, service.Value!.Id, slotStart, "My visit"),
            CancellationToken.None);

        Assert.True(created.IsSuccess);

        var listed = await bookings.ListForCustomerAsync(customerSub, CancellationToken.None);

        Assert.Single(listed);
        Assert.Equal(created.Value!.Id, listed[0].Id);
        Assert.Equal("Fade", listed[0].ServiceName);
        Assert.Equal("Booking Test Shop", listed[0].BusinessName);
        Assert.False(string.IsNullOrWhiteSpace(listed[0].BusinessSlug));
    }

    [Fact]
    public async Task Customer_can_cancel_upcoming_booking()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);

        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var availability = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var service = await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Fade", null, 5000m, "NGN", 30),
            CancellationToken.None);

        await availability.ReplaceWeeklyRulesAsync(
            tenantId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))],
            CancellationToken.None);

        const string customerSub = "auth0|customer-cancel";
        var slotStart = NextMondayAt(new TimeOnly(10, 0));
        var created = await bookings.CreateAsync(
            customerSub,
            new CreateBookingRequest(tenantId, service.Value!.Id, slotStart, null),
            CancellationToken.None);

        Assert.True(created.IsSuccess);

        var cancelled = await bookings.CancelAsync(customerSub, created.Value!.Id, CancellationToken.None);

        Assert.True(cancelled.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, cancelled.Value!.Status);
    }

    [Fact]
    public async Task Business_profile_time_zone_overrides_market_default()
    {
        await using var provider = BuildProvider(defaultTimeZoneId: "UTC");
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(
            scope.ServiceProvider,
            timeZoneId: "Europe/London");

        var scheduling = scope.ServiceProvider.GetRequiredService<ITenantSchedulingTimeZone>();
        var resolved = await scheduling.ForTenantAsync(tenantId, CancellationToken.None);

        Assert.Equal("Europe/London", resolved.TimeZoneId);
    }

    private static DateTimeOffset NextMondayAt(TimeOnly time)
    {
        var today = DateTimeOffset.UtcNow;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0)
        {
            daysUntilMonday = 7;
        }

        var targetDate = today.Date.AddDays(daysUntilMonday);
        return new DateTimeOffset(targetDate + time.ToTimeSpan(), TimeSpan.Zero);
    }

    private static async Task<Guid> SeedVerifiedTenantAsync(
        IServiceProvider provider,
        string? timeZoneId = null)
    {
        var db = provider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Booking Test Shop",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
            VerifiedAt = DateTimeOffset.UtcNow
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "barbers",
            Phone = "+2348012345678",
            Description = "Booking tests",
            UpdatedAt = DateTimeOffset.UtcNow
        });
        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = $"booking-test-{tenantId:N}"[..24],
            Name = "Test Area",
            MarketId = "lagos",
            AddressLine = "1 Test Road",
            Area = "Test Area",
            TimeZoneId = timeZoneId,
            IsPrimary = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private static ServiceProvider BuildProvider(string defaultTimeZoneId = TestTimeZoneId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddSingleton<ICacheService, DistributedCacheService>();
        services.AddSingleton<IDistributedLockProvider, NoOpLockProvider>();
        services.Configure<MarketOptions>(options => options.DefaultTimeZoneId = defaultTimeZoneId);
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<ITenantSchedulingTimeZone, TenantSchedulingTimeZone>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IBookingService, BookingService>();
        return services.BuildServiceProvider();
    }
}
