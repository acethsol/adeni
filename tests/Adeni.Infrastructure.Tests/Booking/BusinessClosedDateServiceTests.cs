namespace Adeni.Infrastructure.Tests.Booking;

using Adeni.Infrastructure.Tests.TestSupport;

using Adeni.Infrastructure.Tests.Subscriptions;
using Adeni.Application.Abstractions;
using Adeni.Application.Booking;
using Adeni.Application.Caching;
using Adeni.Application.Events;
using Adeni.Application.Markets;
using Adeni.Application.Finance;
using Adeni.Application.Reviews;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;
using Adeni.Application.Tenancy;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Tenancy;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Finance;
using Adeni.Infrastructure.Reporting;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Reviews;
using Adeni.Infrastructure.Subscriptions;
using Adeni.Infrastructure.Tests.Catalog;
using Adeni.Infrastructure.Tests.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public sealed class BusinessClosedDateServiceTests
{
    private const string TestTimeZoneId = "UTC";

    [Fact]
    public async Task Create_rejects_overlapping_range()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);
        var closed = scope.ServiceProvider.GetRequiredService<IBusinessClosedDateService>();

        var first = await closed.CreateAsync(
            tenantId,
            new CreateBusinessClosedDateRequest(new DateOnly(2026, 12, 24), new DateOnly(2026, 12, 26), "Holiday"),
            cancellationToken: CancellationToken.None);

        var overlap = await closed.CreateAsync(
            tenantId,
            new CreateBusinessClosedDateRequest(new DateOnly(2026, 12, 26), new DateOnly(2026, 12, 28), null),
            cancellationToken: CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(overlap.IsFailure);
        Assert.Equal(Error.Conflict("x").Code, overlap.Error.Code);
    }

    [Fact]
    public async Task Inclusive_end_date_marks_last_day_closed()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);
        var closed = scope.ServiceProvider.GetRequiredService<IBusinessClosedDateService>();

        await closed.CreateAsync(
            tenantId,
            new CreateBusinessClosedDateRequest(new DateOnly(2026, 12, 24), new DateOnly(2026, 12, 26), null),
            cancellationToken: CancellationToken.None);

        Assert.True(await closed.IsClosedAsync(tenantId, new DateOnly(2026, 12, 24), cancellationToken: CancellationToken.None));
        Assert.True(await closed.IsClosedAsync(tenantId, new DateOnly(2026, 12, 26), cancellationToken: CancellationToken.None));
        Assert.False(await closed.IsClosedAsync(tenantId, new DateOnly(2026, 12, 27), cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task Closed_date_excludes_slots_and_rejects_booking()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);

        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var availability = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var closed = scope.ServiceProvider.GetRequiredService<IBusinessClosedDateService>();

        var service = await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Fade", null, 5000m, "NGN", 30),
            cancellationToken: CancellationToken.None);

        await availability.ReplaceWeeklyRulesAsync(
            tenantId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))],
            cancellationToken: CancellationToken.None);

        var slotStart = NextMondayAt(new TimeOnly(10, 0));
        var closedDay = DateOnly.FromDateTime(slotStart.UtcDateTime.Date);

        await closed.CreateAsync(
            tenantId,
            new CreateBusinessClosedDateRequest(closedDay, closedDay, "Staff day"),
            cancellationToken: CancellationToken.None);

        var slots = await availability.GetAvailableSlotsAsync(
            tenantId,
            service.Value!.Id,
            slotStart.AddHours(-1),
            slotStart.AddHours(4),
            cancellationToken: CancellationToken.None);

        Assert.True(slots.IsSuccess);
        Assert.Empty(slots.Value!);

        var created = await bookings.CreateAsync(
            "auth0|customer-closed",
            new CreateBookingRequest(tenantId, service.Value.Id, slotStart, null),
            cancellationToken: CancellationToken.None);

        Assert.True(created.IsFailure);
        Assert.Equal(ErrorCodes.SlotUnavailable, created.Error.Code);
    }

    [Fact]
    public async Task Create_returns_conflicting_booking_ids_without_cancelling()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);

        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var availability = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var closed = scope.ServiceProvider.GetRequiredService<IBusinessClosedDateService>();

        var service = await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Fade", null, 5000m, "NGN", 30),
            cancellationToken: CancellationToken.None);

        await availability.ReplaceWeeklyRulesAsync(
            tenantId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))],
            cancellationToken: CancellationToken.None);

        var slotStart = NextMondayAt(new TimeOnly(10, 0));
        var booking = await bookings.CreateAsync(
            "auth0|customer-warn",
            new CreateBookingRequest(tenantId, service.Value!.Id, slotStart, null),
            cancellationToken: CancellationToken.None);

        Assert.True(booking.IsSuccess);

        var closedDay = DateOnly.FromDateTime(slotStart.UtcDateTime.Date);
        var result = await closed.CreateAsync(
            tenantId,
            new CreateBusinessClosedDateRequest(closedDay, closedDay, "Renovations"),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.ConflictingBookingIds);
        Assert.Contains(booking.Value!.Id, result.Value.ConflictingBookingIds!);

        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var status = await db.Bookings.AsNoTracking()
            .Where(b => b.Id == booking.Value.Id)
            .Select(b => b.Status)
            .SingleAsync();
        Assert.Equal(BookingStatus.Pending, status);
    }

    [Fact]
    public async Task Delete_reopens_slots()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);

        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var availability = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        var closed = scope.ServiceProvider.GetRequiredService<IBusinessClosedDateService>();

        var service = await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Fade", null, 5000m, "NGN", 30),
            cancellationToken: CancellationToken.None);

        await availability.ReplaceWeeklyRulesAsync(
            tenantId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))],
            cancellationToken: CancellationToken.None);

        var slotStart = NextMondayAt(new TimeOnly(10, 0));
        var closedDay = DateOnly.FromDateTime(slotStart.UtcDateTime.Date);

        var created = await closed.CreateAsync(
            tenantId,
            new CreateBusinessClosedDateRequest(closedDay, closedDay, null),
            cancellationToken: CancellationToken.None);
        Assert.True(created.IsSuccess);

        var before = await availability.GetAvailableSlotsAsync(
            tenantId,
            service.Value!.Id,
            slotStart.AddHours(-1),
            slotStart.AddHours(4),
            cancellationToken: CancellationToken.None);
        Assert.Empty(before.Value!);

        await closed.DeleteAsync(tenantId, created.Value!.Id, CancellationToken.None);

        var after = await availability.GetAvailableSlotsAsync(
            tenantId,
            service.Value.Id,
            slotStart.AddHours(-1),
            slotStart.AddHours(4),
            cancellationToken: CancellationToken.None);
        Assert.True(after.IsSuccess);
        Assert.Contains(after.Value!, s => s.StartAt == slotStart);
    }

    private static DateTimeOffset NextMondayAt(TimeOnly time)
    {
        var utcToday = DateTime.UtcNow.Date;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)utcToday.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0)
        {
            daysUntilMonday = 7;
        }

        var monday = utcToday.AddDays(daysUntilMonday);
        return new DateTimeOffset(monday.Year, monday.Month, monday.Day, time.Hour, time.Minute, 0, TimeSpan.Zero);
    }

    private static async Task<Guid> SeedVerifiedTenantAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Closed Dates Shop",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
            VerifiedAt = DateTimeOffset.UtcNow
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "barbers",
            Phone = "+2348012345678",
            Description = "Closed date tests",
            UpdatedAt = DateTimeOffset.UtcNow
        });
        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = $"closed-loc-{tenantId:N}"[..24],
            Name = "Test Area",
            MarketId = "lagos",
            AddressLine = "1 Test Road",
            Area = "Test Area",
            TimeZoneId = TestTimeZoneId,
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
        services.Configure<MarketOptions>(options => options.DefaultTimeZoneId = TestTimeZoneId);
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddCategoryWorkflowCatalog();
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<ITenantSchedulingTimeZone, TenantSchedulingTimeZone>();
        services.AddScoped<IBusinessClosedDateService, BusinessClosedDateService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddEntitlementsModule();
        services.Configure<FinanceOptions>(options =>
        {
            options.Provider = "Stub";
            options.StubCheckoutBaseUrl = "/checkout/stub";
        });
        AuditTestDoubles.Register(services);
        services.AddSingleton<IMarketCatalog, ClosedTestMarketCatalog>();
        services.AddScoped<IPlatformFeeCalculator, PlatformFeeCalculator>();
        services.AddScoped<IPaymentProvider, StubPaymentProvider>();
        services.AddBookingTestRealtime();
        services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
        services.AddScoped<IStaffCompensationService, StaffCompensationService>();
        services.AddSingleton<Application.Notifications.INotificationDispatcher, ClosedTestNotifications>();
        services.AddReportingModule();
        services.AddScoped<IBillingDocumentService, BillingDocumentService>();
        services.AddAdeniDomainEvents();
        services.AddScoped<ITenantCustomerService, TenantCustomerService>();
        services.AddScoped<IQueueService, NoOpQueueService>();
        services.AddScoped<IBookingService, BookingService>();
        return services.BuildServiceProvider();
    }

    private sealed class ClosedTestNotifications : Application.Notifications.INotificationDispatcher
    {
        public Task SendAsync(
            Application.Notifications.NotificationMessage message,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class ClosedTestCorrelationContext : ICorrelationContext
    {
        public string CorrelationId { get; } = "closed-dates";
        public void Set(string correlationId) { }
    }

    private sealed class ClosedTestAuditLogWriter : IAuditLogWriter
    {
        public Task WriteAsync(Domain.Auditing.AuditEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class ClosedTestMarketCatalog : IMarketCatalog
    {
        public IReadOnlyList<MarketDefinition> List() =>
            [new("lagos", "Lagos", "NG", "NGN", "Africa/Lagos", new(0, 0), ["en"], true, null, 0m)];

        public IReadOnlyList<MarketDefinition> ListLive() => List();

        public MarketDefinition? GetById(string marketId) =>
            List().FirstOrDefault(m => m.Id == marketId);

        public bool IsValid(string? marketId) => GetById(marketId ?? string.Empty) is not null;

        public string Normalize(string marketId) => marketId.ToLowerInvariant();
    }
}
