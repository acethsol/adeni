namespace Adeni.Infrastructure.Tests.Events;

using Adeni.Infrastructure.Tests.Finance;
using Adeni.Infrastructure.Tests.TestSupport;

using Adeni.Infrastructure.Tests.Subscriptions;
using Adeni.Application.Abstractions;
using Adeni.Application.Booking;
using Adeni.Application.Caching;
using Adeni.Application.Events;
using Adeni.Application.Markets;
using Adeni.Application.Notifications;
using Adeni.Application.Finance;
using Adeni.Application.Reviews;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Booking;
using Adeni.Domain.Tenancy;
using Adeni.Application.Tenancy;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Tenancy;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Tests.Notifications;
using Adeni.Infrastructure.Notifications;
using Adeni.Infrastructure.Finance;
using Adeni.Infrastructure.Reporting;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Reviews;
using Adeni.Infrastructure.Subscriptions;
using Adeni.Infrastructure.Tests.Booking;
using Adeni.Infrastructure.Tests.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var bookingRow = await db.Bookings.AsNoTracking().FirstAsync(x => x.Id == created.Value!.Id);
        var customerRow = await db.Customers.FirstAsync(x => x.Id == bookingRow.CustomerId);
        customerRow.Email = "customer1@example.com";
        await db.SaveChangesAsync(CancellationToken.None);

        var accepted = await bookings.AcceptAsync(tenantId, created.Value!.Id, cancellationToken: CancellationToken.None);

        Assert.True(accepted.IsSuccess);
        Assert.Equal(BookingStatus.Confirmed, accepted.Value!.Status);

        var sink = scope.ServiceProvider.GetRequiredService<NotificationTestSink>();
        Assert.Single(sink.Messages);
        Assert.Equal(NotificationTemplateIds.BookingConfirmed, sink.Messages[0].TemplateId);
        Assert.Equal("customer1@example.com", sink.Messages[0].RecipientKey);
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
        services.AddScoped<INotificationRecipientResolver, NotificationRecipientResolver>();
        services.AddScoped<IDomainEventHandler<Domain.Booking.Events.BookingConfirmed>, BookingNotificationHandler>();
        services.AddScoped<IDomainEventHandler<Domain.Booking.Events.BookingRejected>, BookingNotificationHandler>();
        services.AddScoped<IDomainEventHandler<Domain.Booking.Events.BookingCancelled>, BookingNotificationHandler>();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddCategoryWorkflowCatalog();
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp =>
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
        services.AddSingleton<IMarketCatalog, EventTestMarketCatalog>();
        services.AddScoped<IPlatformFeeCalculator, PlatformFeeCalculator>();
        services.AddScoped<IPaymentProvider, StubPaymentProvider>();
        services.AddBookingTestRealtime();
        services.AddCheckoutRealtimeTokens();
        services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
        services.AddScoped<IStaffCompensationService, StaffCompensationService>();
        services.AddReportingModule();
        services.AddScoped<IBillingDocumentService, BillingDocumentService>();
        services.AddAdeniDomainEvents();
        services.AddScoped<ITenantCustomerService, TenantCustomerService>();
        services.AddScoped<IQueueService, NoOpQueueService>();
        services.AddScoped<IBookingService, BookingService>();
        return services.BuildServiceProvider();
    }

    private sealed class EventTestCorrelationContext : ICorrelationContext
    {
        public string CorrelationId { get; } = "event-test";
        public void Set(string correlationId) { }
    }

    private sealed class EventTestAuditLogWriter : IAuditLogWriter
    {
        public Task WriteAsync(Domain.Auditing.AuditEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class EventTestMarketCatalog : IMarketCatalog
    {
        public IReadOnlyList<MarketDefinition> List() =>
            [new("lagos", "Lagos", "NG", "NGN", "Africa/Lagos", new(0, 0), ["en"], true, null, 0m)];

        public IReadOnlyList<MarketDefinition> ListLive() => List();

        public MarketDefinition? GetById(string marketId) =>
            List().FirstOrDefault(m => m.Id == marketId);

        public bool IsValid(string? marketId) => GetById(marketId ?? string.Empty) is not null;

        public string Normalize(string marketId) => marketId.ToLowerInvariant();
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
