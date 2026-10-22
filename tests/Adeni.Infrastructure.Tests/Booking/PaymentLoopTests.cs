namespace Adeni.Infrastructure.Tests.Booking;

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
using Adeni.Domain.Finance;
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

public sealed class PaymentLoopTests
{
    [Fact]
    public async Task Check_in_complete_mark_paid_at_till()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var bookingId = await CreateConfirmedBookingAsync(scope.ServiceProvider, tenantId);

        var checkedIn = await bookings.CheckInAsync(tenantId, bookingId);
        Assert.True(checkedIn.IsSuccess, checkedIn.Error?.Message);
        Assert.Equal(BookingStatus.CheckedIn, checkedIn.Value!.Status);

        var completed = await bookings.CompleteAsync(tenantId, bookingId);
        Assert.True(completed.IsSuccess, completed.Error?.Message);
        Assert.Equal(BookingStatus.Completed, completed.Value!.Status);

        var paid = await bookings.MarkPaidAsync(
            tenantId,
            bookingId,
            new MarkBookingPaidRequest("TILL-1", "Cash"));
        Assert.True(paid.IsSuccess, paid.Error?.Message);
        Assert.Equal(BookingStatus.Paid, paid.Value!.Status);
        Assert.Equal(nameof(BookingSettlementSource.ExternalTill), paid.Value.SettlementSource);
        Assert.Equal("TILL-1", paid.Value.ExternalPaymentReference);
        Assert.NotNull(paid.Value.PaidAt);
    }

    [Fact]
    public async Task Complete_auto_pays_when_deposit_covers_total()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var bookingId = await CreateConfirmedBookingAsync(scope.ServiceProvider, tenantId, price: 5000m);

        db.PaymentIntents.Add(new PaymentIntentRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BookingId = bookingId,
            Amount = 5000m,
            Currency = "NGN",
            Status = PaymentIntentStatus.Completed,
            Type = PaymentIntentType.Deposit,
            ProviderReference = "stub_full_deposit",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        Assert.True((await bookings.CheckInAsync(tenantId, bookingId)).IsSuccess);
        var completed = await bookings.CompleteAsync(tenantId, bookingId);

        Assert.True(completed.IsSuccess, completed.Error?.Message);
        Assert.Equal(BookingStatus.Paid, completed.Value!.Status);
        Assert.Equal(nameof(BookingSettlementSource.Online), completed.Value.SettlementSource);
    }

    [Fact]
    public async Task Collect_balance_creates_checkout_for_remaining()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var bookingId = await CreateConfirmedBookingAsync(scope.ServiceProvider, tenantId, price: 8000m);

        Assert.True((await bookings.CheckInAsync(tenantId, bookingId)).IsSuccess);
        Assert.True((await bookings.CompleteAsync(tenantId, bookingId)).IsSuccess);

        var collect = await bookings.CollectBalanceAsync(tenantId, bookingId);
        Assert.True(collect.IsSuccess, collect.Error?.Message);
        Assert.Equal(8000m, collect.Value!.Amount);
        Assert.StartsWith("/checkout/stub/", collect.Value.CheckoutUrl);
    }

    [Fact]
    public async Task GetReceiptAsync_returns_summary_for_completed_payment()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);
        var paymentId = Guid.NewGuid();
        db.PaymentIntents.Add(new PaymentIntentRecord
        {
            Id = paymentId,
            TenantId = tenantId,
            Amount = 2500m,
            Currency = "NGN",
            Status = PaymentIntentStatus.Completed,
            Type = PaymentIntentType.Link,
            Description = "Balance — Fade",
            ProviderReference = "stub_receipt",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var orchestrator = scope.ServiceProvider.GetRequiredService<IPaymentOrchestrator>();
        var receipt = await orchestrator.GetReceiptAsync(paymentId);

        Assert.True(receipt.IsSuccess, receipt.Error?.Message);
        Assert.Equal("Booking Test Shop", receipt.Value!.BusinessName);
        Assert.Equal(2500m, receipt.Value.Amount);
        Assert.Equal("Completed", receipt.Value.Status);
        Assert.Equal("Online payment", receipt.Value.Method);
    }

    private static async Task<Guid> CreateConfirmedBookingAsync(
        IServiceProvider services,
        Guid tenantId,
        decimal price = 5000m)
    {
        var catalog = services.GetRequiredService<IServiceCatalogService>();
        var availability = services.GetRequiredService<IAvailabilityService>();
        var bookings = services.GetRequiredService<IBookingService>();

        var service = await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Fade", null, price, "NGN", 30),
            cancellationToken: CancellationToken.None);

        await availability.ReplaceWeeklyRulesAsync(
            tenantId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))],
            cancellationToken: CancellationToken.None);

        var slotStart = NextMondayAt(new TimeOnly(10, 0));
        var created = await bookings.CreateAsync(
            "auth0|loop-customer",
            new CreateBookingRequest(tenantId, service.Value!.Id, slotStart, null),
            cancellationToken: CancellationToken.None);
        Assert.True(created.IsSuccess, created.Error?.Message);

        var accepted = await bookings.AcceptAsync(tenantId, created.Value!.Id);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        return created.Value.Id;
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
            Name = "Booking Test Shop",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
            VerifiedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "barbers",
            Phone = "+2348012345678",
            Description = "Loop tests",
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = $"loop-test-{tenantId:N}"[..24],
            Name = "Test Area",
            MarketId = "lagos",
            AddressLine = "1 Test Road",
            Area = "Test Area",
            IsPrimary = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
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
        services.Configure<MarketOptions>(options => options.DefaultTimeZoneId = "UTC");
        services.Configure<FinanceOptions>(options =>
        {
            options.Provider = "Stub";
            options.StubCheckoutBaseUrl = "/checkout/stub";
        });
        services.AddInlineNotificationJobScheduling();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddCategoryWorkflowCatalog();
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddSingleton<ICorrelationContext, TestCorrelationContext>();
        services.AddSingleton<IAuditLogWriter, NoOpAuditLogWriter>();
        services.AddSingleton<IMarketCatalog, LagosMarketCatalog>();
        services.AddScoped<IPlatformFeeCalculator, PlatformFeeCalculator>();
        services.AddScoped<IPaymentProvider, StubPaymentProvider>();
        services.AddBookingTestRealtime();
        services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
        services.AddScoped<IStaffCompensationService, StaffCompensationService>();
        services.AddSingleton<INotificationDispatcher, LoopNotificationDispatcher>();
        services.AddReportingModule();
        services.AddScoped<IBillingDocumentService, BillingDocumentService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<ITenantSchedulingTimeZone, TenantSchedulingTimeZone>();
        services.AddScoped<IBusinessClosedDateService, BusinessClosedDateService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddEntitlementsModule();
        services.AddScoped<ITenantCustomerService, TenantCustomerService>();
        services.AddScoped<IQueueService, NoOpQueueService>();
        services.AddScoped<IBookingService, BookingService>();
        return services.BuildServiceProvider();
    }

    private sealed class LoopNotificationDispatcher : INotificationDispatcher
    {
        public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class LagosMarketCatalog : IMarketCatalog
    {
        public IReadOnlyList<MarketDefinition> List() =>
            [new("lagos", "Lagos", "NG", "NGN", "Africa/Lagos", new(0, 0), ["en"], true, null, 0m)];

        public IReadOnlyList<MarketDefinition> ListLive() => List();

        public MarketDefinition? GetById(string marketId) =>
            List().FirstOrDefault(m => m.Id == marketId);

        public bool IsValid(string? marketId) => GetById(marketId ?? string.Empty) is not null;

        public string Normalize(string marketId) => marketId.ToLowerInvariant();
    }

    private sealed class TestCorrelationContext : ICorrelationContext
    {
        public string CorrelationId { get; } = "payment-loop-test";
        public void Set(string correlationId) { }
    }

    private sealed class NoOpAuditLogWriter : IAuditLogWriter
    {
        public Task WriteAsync(Domain.Auditing.AuditEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
