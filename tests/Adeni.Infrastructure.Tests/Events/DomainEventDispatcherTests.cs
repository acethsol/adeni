namespace Adeni.Infrastructure.Tests.Events;

using Adeni.Application.Booking;
using Adeni.Application.Caching;
using Adeni.Application.Events;
using Adeni.Application.Markets;
using Adeni.Application.Notifications;
using Adeni.Application.Reviews;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Booking;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Notifications;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Reviews;
using Adeni.Infrastructure.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task Accept_booking_dispatches_BookingConfirmed_and_sends_notification()
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
            cancellationToken: CancellationToken.None);

        await availability.ReplaceWeeklyRulesAsync(
            tenantId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))],
            cancellationToken: CancellationToken.None);

        var slotStart = NextMondayAt(new TimeOnly(10, 0));
        var created = await bookings.CreateAsync(
            "auth0|customer-1",
            new CreateBookingRequest(tenantId, service.Value!.Id, slotStart, null),
            cancellationToken: CancellationToken.None);

        var accepted = await bookings.AcceptAsync(tenantId, created.Value!.Id, CancellationToken.None);

        Assert.True(accepted.IsSuccess);
        Assert.Equal(BookingStatus.Confirmed, accepted.Value!.Status);

        var sink = scope.ServiceProvider.GetRequiredService<NotificationTestSink>();
        Assert.Single(sink.Messages);
        Assert.Equal("Booking confirmed", sink.Messages[0].Subject);
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

    private static async Task<Guid> SeedVerifiedTenantAsync(IServiceProvider provider)
    {
        var db = provider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Event Test Shop",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
            VerifiedAt = DateTimeOffset.UtcNow
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "barbers",
            Phone = "+2348012345678",
            Description = "Event tests",
            UpdatedAt = DateTimeOffset.UtcNow
        });
        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = $"event-test-{tenantId:N}"[..24],
            Name = "Test Area",
            MarketId = "lagos",
            AddressLine = "1 Test Road",
            Area = "Test Area",
            TimeZoneId = "UTC",
            IsPrimary = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddSingleton<ICacheService, DistributedCacheService>();
        services.AddSingleton<IDistributedLockProvider, NoOpLockProvider>();
        services.AddSingleton<NotificationTestSink>();
        services.AddScoped<INotificationDispatcher, TestNotificationDispatcher>();
        services.Configure<MarketOptions>(options => options.DefaultTimeZoneId = "UTC");
        services.AddAdeniDomainEvents();
        services.AddScoped<IDomainEventHandler<Domain.Booking.Events.BookingConfirmed>, BookingNotificationHandler>();
        services.AddScoped<IDomainEventHandler<Domain.Booking.Events.BookingRejected>, BookingNotificationHandler>();
        services.AddScoped<IDomainEventHandler<Domain.Booking.Events.BookingCancelled>, BookingNotificationHandler>();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<ITenantSchedulingTimeZone, TenantSchedulingTimeZone>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IEntitlementsService, EntitlementsService>();
        services.AddScoped<IBookingService, BookingService>();
        return services.BuildServiceProvider();
    }

    private sealed class NotificationTestSink
    {
        public List<NotificationMessage> Messages { get; } = [];
    }

    private sealed class TestNotificationDispatcher(NotificationTestSink sink) : INotificationDispatcher
    {
        public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            sink.Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
