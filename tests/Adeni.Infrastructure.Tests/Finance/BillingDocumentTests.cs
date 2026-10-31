namespace Adeni.Infrastructure.Tests.Finance;

using Adeni.Application.Abstractions;
using Adeni.Application.Events;
using Adeni.Application.Markets;
using Adeni.Application.Notifications;
using Adeni.Application.Finance;
using Adeni.Application.Subscriptions;
using Adeni.Infrastructure.Subscriptions;
using Adeni.Domain.Booking;
using Adeni.Domain.Finance;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Tests.Notifications;
using Adeni.Infrastructure.Finance;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Reporting;
using Adeni.Infrastructure.Tests.Booking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class BillingDocumentTests
{
    [Fact]
    public async Task IssueReceiptForPayment_is_idempotent_and_numbered()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var docs = scope.ServiceProvider.GetRequiredService<IBillingDocumentService>();
        var tenantId = await SeedTenantAsync(db);
        var paymentId = Guid.NewGuid();
        db.PaymentIntents.Add(new PaymentIntentRecord
        {
            Id = paymentId,
            TenantId = tenantId,
            Amount = 4500m,
            Currency = "NGN",
            Status = PaymentIntentStatus.Completed,
            Type = PaymentIntentType.Link,
            Description = "Balance — Fade",
            ProviderReference = "stub_doc_1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var first = await docs.IssueReceiptForPaymentAsync(paymentId);
        var second = await docs.IssueReceiptForPaymentAsync(paymentId);

        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.StartsWith("RCP-", first.Value.DocumentNumber);
        Assert.Equal("issued", first.Value.Status);

        var pdf = await docs.GetPdfAsync(first.Value.Id);
        Assert.True(pdf.IsSuccess, pdf.Error?.Message);
        Assert.True(pdf.Value!.Length > 100);
        Assert.Equal(0x25, pdf.Value[0]); // '%'
        Assert.Equal((byte)'P', pdf.Value[1]);
        Assert.Equal((byte)'D', pdf.Value[2]);
        Assert.Equal((byte)'F', pdf.Value[3]);
    }

    [Fact]
    public async Task Till_receipt_issues_without_payment_intent()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var docs = scope.ServiceProvider.GetRequiredService<IBillingDocumentService>();
        var tenantId = await SeedTenantAsync(db);
        var bookingId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        db.ServiceOfferings.Add(new ServiceOffering
        {
            Id = serviceId,
            TenantId = tenantId,
            Name = "Silk press",
            PriceAmount = 8000m,
            Currency = "NGN",
            DurationMinutes = 60,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        var customerId = Guid.NewGuid();
        db.Customers.Add(new Domain.Identity.Customer
        {
            Id = customerId,
            Auth0Sub = "auth0|doc-customer",
            Name = "Ada Okoro",
            Email = "ada@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.Bookings.Add(new BookingRecord
        {
            Id = bookingId,
            TenantId = tenantId,
            ServiceOfferingId = serviceId,
            CustomerId = customerId,
            StartAt = DateTimeOffset.UtcNow.AddHours(1),
            EndAt = DateTimeOffset.UtcNow.AddHours(2),
            Status = BookingStatus.Paid,
            SettlementSource = BookingSettlementSource.ExternalTill,
            PaidAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var receipt = await docs.IssueReceiptForTillPaymentAsync(tenantId, bookingId, "TILL-9");
        Assert.True(receipt.IsSuccess, receipt.Error?.Message);
        Assert.Null(receipt.Value!.PaymentIntentId);
        Assert.Equal(8000m, receipt.Value.Total);
        Assert.Contains("till", receipt.Value.Method!, StringComparison.OrdinalIgnoreCase);

        var byToken = await docs.GetByPublicTokenAsync(receipt.Value.PublicToken);
        Assert.True(byToken.IsSuccess);
        Assert.Equal(receipt.Value.Id, byToken.Value!.Id);
    }

    [Fact]
    public async Task Email_dispatches_notification_for_issued_document()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var docs = scope.ServiceProvider.GetRequiredService<IBillingDocumentService>();
        var sink = scope.ServiceProvider.GetRequiredService<NotificationSink>();
        var tenantId = await SeedTenantAsync(db);
        var paymentId = Guid.NewGuid();
        db.PaymentIntents.Add(new PaymentIntentRecord
        {
            Id = paymentId,
            TenantId = tenantId,
            Amount = 1000m,
            Currency = "NGN",
            Status = PaymentIntentStatus.Completed,
            Type = PaymentIntentType.Deposit,
            CustomerEmail = "guest@example.com",
            ProviderReference = "stub_doc_email",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var issued = await docs.IssueReceiptForPaymentAsync(paymentId);
        Assert.True(issued.IsSuccess, issued.Error?.Message);

        var emailed = await docs.EmailAsync(
            tenantId,
            issued.Value!.Id,
            new EmailBillingDocumentRequest());
        Assert.True(emailed.IsSuccess, emailed.Error?.Message);
        Assert.Contains(sink.Messages, m => m.RecipientKey == "guest@example.com");
    }

    private static async Task<Guid> SeedTenantAsync(AdeniDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Doc Test Salon",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
            VerifiedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.Configure<FinanceOptions>(o =>
        {
            o.Provider = "Stub";
            o.StubCheckoutBaseUrl = "/checkout/stub";
        });
        services.AddSingleton<ICorrelationContext, TestCorrelationContext>();
        services.AddSingleton<IAuditLogWriter, NoOpAudit>();
        services.AddSingleton<IMarketCatalog, LagosCatalog>();
        services.AddScoped<IPricingCatalogService, PricingCatalogService>();
        services.AddSingleton<NotificationSink>();
        services.AddSingleton<INotificationDispatcher>(sp =>
            new SinkNotificationDispatcher(sp.GetRequiredService<NotificationSink>()));
        services.AddAdeniDomainEvents();
        services.AddScoped<IPlatformFeeCalculator, PlatformFeeCalculator>();
        services.AddScoped<IPaymentProvider, StubPaymentProvider>();
        services.AddBookingTestRealtime();
        services.AddCheckoutRealtimeTokens();
        services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
        services.AddSingleton<Adeni.Application.Storage.IFileStorage, Adeni.Infrastructure.Tests.Storage.FakeFileStorage>();
        services.AddReportingModule();
        services.AddScoped<IBillingDocumentService, BillingDocumentService>();
        services.AddSingleton(NullLogger<PaymentBookingHandler>.Instance);
        return services.BuildServiceProvider();
    }

    private sealed class NotificationSink
    {
        public List<NotificationMessage> Messages { get; } = [];
    }

    private sealed class SinkNotificationDispatcher(NotificationSink sink) : INotificationDispatcher
    {
        public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            sink.Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class TestCorrelationContext : ICorrelationContext
    {
        public string CorrelationId { get; } = "billing-doc-test";
        public void Set(string correlationId) { }
    }

    private sealed class NoOpAudit : IAuditLogWriter
    {
        public Task WriteAsync(Domain.Auditing.AuditEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class LagosCatalog : IMarketCatalog
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
