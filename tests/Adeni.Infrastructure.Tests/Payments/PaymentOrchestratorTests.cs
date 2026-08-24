namespace Adeni.Infrastructure.Tests.Payments;

using Adeni.Application.Abstractions;
using Adeni.Application.Events;
using Adeni.Application.Payments;
using Adeni.Domain.Payments;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Payments;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class PaymentOrchestratorTests
{
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
        Assert.Single(result.Value!);
        Assert.Equal("stub_ledger", result.Value![0].ProviderReference);
    }

    private static PaymentOrchestrator CreateOrchestrator(AdeniDbContext db)
    {
        var paymentsOptions = Options.Create(new PaymentsOptions
        {
            Provider = "Stub",
            StubCheckoutBaseUrl = "/checkout/stub",
        });

        return new PaymentOrchestrator(
            db,
            new StubPaymentProvider(db, paymentsOptions),
            new PlatformFeeCalculator(new TestMarketCatalog()),
            new NoOpAuditLogWriter(),
            new TestCorrelationContext(),
            new DomainEventCollector(),
            paymentsOptions);
    }

    private static AdeniDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AdeniDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AdeniDbContext(options, new TestTenantContext());
    }

    private sealed class TestMarketCatalog : Application.Markets.IMarketCatalog
    {
        public IReadOnlyList<Application.Markets.MarketDefinition> List() =>
            [new("lagos", "Lagos", "NG", "NGN", "Africa/Lagos", new(0, 0), ["en"], true, null, 0m)];

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
