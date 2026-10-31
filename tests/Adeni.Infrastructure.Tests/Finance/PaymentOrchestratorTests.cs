namespace Adeni.Infrastructure.Tests.Finance;

using Adeni.Application.Abstractions;
using Adeni.Application.Realtime;
using Adeni.Application.Events;
using Adeni.Application.Finance;
using Adeni.Infrastructure.Booking;
using Microsoft.Extensions.Hosting;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Domain.Finance;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class PaymentOrchestratorTests
{
    [Fact]
    public async Task InitializeAsync_externalProviderCheckoutUrl_omitsRealtimeQueryParam()
    {
        await using var db = CreateDb();
        var paymentsOptions = Options.Create(new FinanceOptions
        {
            Provider = "Paystack",
            StubCheckoutBaseUrl = "/checkout/stub",
        });
        var callbackValidator = new PaymentCallbackUrlValidator(paymentsOptions, new TestHostEnvironment());
        var orchestrator = new PaymentOrchestrator(
            db,
            new ExternalCheckoutPaymentProvider(),
            new PlatformFeeCalculator(new TestMarketCatalog(), new TestPricingCatalog()),
            new NoOpAuditLogWriter(),
            new TestCorrelationContext(),
            new DomainEventCollector(),
            paymentsOptions,
            new NoOpTenantRealtimePublisher(),
            new PaymentCheckoutRealtimeTokenService(paymentsOptions),
            new CustomerPaymentAccessQuery(db),
            new BookingDepositCalculator(db),
            callbackValidator);

        var result = await orchestrator.InitializeAsync(
            new InitializePaymentRequest(
                Guid.NewGuid(),
                BookingId: null,
                Amount: 2500m,
                Currency: "NGN",
                Type: "link",
                Description: "External"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.CheckoutRealtimeToken);
        Assert.DoesNotContain("rt=", result.Value!.CheckoutUrl, StringComparison.Ordinal);
        Assert.StartsWith("https://checkout.paystack.com/", result.Value!.CheckoutUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitializeAsync_persists_stub_payment_intent()
    {
        await using var db = CreateDb();
        var orchestrator = CreateOrchestrator(db);

        var result = await orchestrator.InitializeAsync(
            new InitializePaymentRequest(
                Guid.NewGuid(),
                BookingId: null,
                Amount: 2500m,
                Currency: "NGN",
                Type: "link",
                Description: "Test link"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("pending", result.Value!.Status);
        Assert.StartsWith("/checkout/stub/", result.Value!.CheckoutUrl);
        Assert.Contains("rt=", result.Value!.CheckoutUrl, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(result.Value!.CheckoutRealtimeToken));

        var stored = await db.PaymentIntents.SingleAsync();
        Assert.Equal(2500m, stored.Amount);
        Assert.Equal(PaymentIntentType.Link, stored.Type);
    }

    [Fact]
    public async Task ConfirmStubCheckoutAsync_marks_payment_completed()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var reference = "stub_test_ref";
        db.PaymentIntents.Add(new PaymentIntentRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Amount = 1000m,
            Currency = "NGN",
            Status = PaymentIntentStatus.Pending,
            ProviderReference = reference,
            Type = PaymentIntentType.Deposit,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(db);
        var result = await orchestrator.ConfirmStubCheckoutAsync(reference);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("completed", result.Value!.Status);

        var stored = await db.PaymentIntents.SingleAsync();
        Assert.Equal(PaymentIntentStatus.Completed, stored.Status);
    }

    [Fact]
    public async Task ListLedgerAsync_returns_tenant_payments()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.PaymentIntents.Add(new PaymentIntentRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Amount = 500m,
            Currency = "NGN",
            Status = PaymentIntentStatus.Completed,
            ProviderReference = "stub_ledger",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var orchestrator = CreateOrchestrator(db);
        var result = await orchestrator.ListLedgerAsync(tenantId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value!.Items);
        Assert.Equal("stub_ledger", result.Value!.Items[0].ProviderReference);
    }

    private static PaymentOrchestrator CreateOrchestrator(AdeniDbContext db)
    {
        var paymentsOptions = Options.Create(new FinanceOptions
        {
            Provider = "Stub",
            StubCheckoutBaseUrl = "/checkout/stub",
        });

        var callbackValidator = new PaymentCallbackUrlValidator(
            paymentsOptions,
            new TestHostEnvironment());

        return new PaymentOrchestrator(
            db,
            new StubPaymentProvider(db, paymentsOptions),
            new PlatformFeeCalculator(new TestMarketCatalog(), new TestPricingCatalog()),
            new NoOpAuditLogWriter(),
            new TestCorrelationContext(),
            new DomainEventCollector(),
            paymentsOptions,
            new NoOpTenantRealtimePublisher(),
            new PaymentCheckoutRealtimeTokenService(paymentsOptions),
            new CustomerPaymentAccessQuery(db),
            new BookingDepositCalculator(db),
            callbackValidator);
    }

    private sealed class ExternalCheckoutPaymentProvider : IPaymentProvider
    {
        public string ProviderName => "Paystack";

        public Task<Result<ProviderInitializeResult>> InitializeAsync(
            ProviderInitializeRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new ProviderInitializeResult(
                $"https://checkout.paystack.com/{request.ProviderReference}",
                request.ProviderReference)));

        public Task<Result<ProviderPaymentStatus>> GetProviderStatusAsync(
            string providerReference,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<PaymentWebhookPayload>> ParseWebhookAsync(
            string rawBody,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<ProviderRefundResult>> RefundAsync(
            string providerReference,
            decimal? amount,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class TestHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static AdeniDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AdeniDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AdeniDbContext(options, new TestTenantContext());
    }

    private sealed class TestPricingCatalog : IPricingCatalogService
    {
        public Task EnsureSeededAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public string GetActiveVersionCode() => "test";

        public decimal? GetMonthlyMajor(string? countryCode, SubscriptionSku sku) => null;

        public decimal GetPlatformFeePercent(string? countryCode) =>
            string.Equals(countryCode, "NG", StringComparison.OrdinalIgnoreCase) ? 2.5m : 0m;

        public bool GetSelfServeEnabled(string? countryCode) => true;

        public Task<Domain.Common.Result<PublicCountryPricingDto>> GetPublicCountryPricingAsync(
            string? countryCode,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Domain.Common.Result<IReadOnlyList<PricingCatalogVersionSummaryDto>>> ListVersionsAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Domain.Common.Result<PricingCatalogVersionDetailDto>> GetVersionAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Domain.Common.Result<PricingCatalogVersionDetailDto>> CreateDraftVersionAsync(
            CreatePricingCatalogVersionRequest request,
            string adminId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Domain.Common.Result<PricingCatalogVersionDetailDto>> UpsertEntriesAsync(
            string code,
            UpsertPricingCatalogEntriesRequest request,
            string adminId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Domain.Common.Result<PricingCatalogVersionDetailDto>> ActivateVersionAsync(
            string code,
            string adminId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public void PlatformFeeCalculator_reads_catalog_percent_by_market_country()
    {
        var calculator = new PlatformFeeCalculator(new TestMarketCatalog(), new TestPricingCatalog());
        Assert.Equal(2.5m, calculator.GetFeePercent("lagos"));
        Assert.Equal(0m, calculator.GetFeePercent("ottawa"));
    }

    private sealed class TestMarketCatalog : Application.Markets.IMarketCatalog
    {
        public IReadOnlyList<Application.Markets.MarketDefinition> List() =>
            [
                new("lagos", "Lagos", "NG", "NGN", "Africa/Lagos", new(0, 0), ["en"], true, null, 0m),
                new("ottawa", "Ottawa", "CA", "CAD", "America/Toronto", new(0, 0), ["en"], true, null, 0m),
            ];

        public IReadOnlyList<Application.Markets.MarketDefinition> ListLive() => List();

        public Application.Markets.MarketDefinition? GetById(string marketId) =>
            List().FirstOrDefault(m => m.Id == marketId);

        public bool IsValid(string? marketId) => GetById(marketId ?? string.Empty) is not null;

        public string Normalize(string marketId) => marketId.ToLowerInvariant();
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Domain.Tenancy.TenantId? CurrentTenantId => null;
        public bool IsTenantFilterActive => false;
        public void EnableTenantFilter(Domain.Tenancy.TenantId tenantId) { }
        public void DisableTenantFilter() { }
        public void Set(Domain.Tenancy.TenantId tenantId) { }
        public void Clear() { }
    }

    private sealed class TestCorrelationContext : ICorrelationContext
    {
        public string CorrelationId { get; } = "test-correlation";
        public void Set(string correlationId) { }
    }

    private sealed class NoOpAuditLogWriter : IAuditLogWriter
    {
        public Task WriteAsync(Domain.Auditing.AuditEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
