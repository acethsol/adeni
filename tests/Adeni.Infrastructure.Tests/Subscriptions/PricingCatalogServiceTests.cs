namespace Adeni.Infrastructure.Tests.Subscriptions;

using Adeni.Application.Abstractions;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Auditing;
using Adeni.Domain.Subscriptions;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Subscriptions;
using Microsoft.EntityFrameworkCore;

public sealed class PricingCatalogServiceTests
{
    [Fact]
    public async Task EnsureSeededAsync_creates_launch_version()
    {
        await using var db = BuildDb();
        var service = CreateService(db);

        await service.EnsureSeededAsync();

        var version = await db.PricingCatalogVersions
            .Include(v => v.Entries)
            .SingleAsync();

        Assert.Equal(PricingCatalogMetadata.ActiveVersionCode, version.Code);
        Assert.Equal(PricingCatalogVersionStatus.Active, version.Status);
        Assert.Equal(9, version.Entries.Count);
    }

    [Fact]
    public async Task GetPublicCountryPricingAsync_returns_ng_prices_after_seed()
    {
        await using var db = BuildDb();
        var service = CreateService(db);

        var result = await service.GetPublicCountryPricingAsync("NG");

        Assert.True(result.IsSuccess);
        var pricing = result.Value!;
        Assert.Equal("NG", pricing.CountryCode);
        Assert.Equal(PricingCatalogMetadata.ActiveVersionCode, pricing.PricingCatalogVersion);
        Assert.Contains(pricing.Skus, s => s.Sku == "professional" && s.MonthlyAmountMajor == 55_000m);
    }

    [Fact]
    public async Task ActivateVersionAsync_swaps_active_flag_and_retires_prior()
    {
        await using var db = BuildDb();
        var service = CreateService(db);
        await service.EnsureSeededAsync();

        var draftCode = "2026-test-draft";
        var create = await service.CreateDraftVersionAsync(
            new CreatePricingCatalogVersionRequest(
                draftCode,
                DateTimeOffset.Parse("2026-11-01T00:00:00Z"),
                null,
                CloneFromActive: true),
            "admin-test");

        Assert.True(create.IsSuccess);
        Assert.Equal("draft", create.Value!.Status);
        Assert.False(create.Value.IsActive);

        var activate = await service.ActivateVersionAsync(draftCode, "admin-test");
        Assert.True(activate.IsSuccess);
        Assert.True(activate.Value!.IsActive);
        Assert.Equal("active", activate.Value.Status);

        var versions = await db.PricingCatalogVersions.AsNoTracking().ToListAsync();
        Assert.Single(versions, v => v.Status == PricingCatalogVersionStatus.Active);
        Assert.Equal(draftCode, versions.Single(v => v.Status == PricingCatalogVersionStatus.Active).Code);

        var prior = versions.Single(v => v.Code == PricingCatalogMetadata.ActiveVersionCode);
        Assert.Equal(PricingCatalogVersionStatus.Retired, prior.Status);
        Assert.NotNull(prior.EffectiveTo);

        Assert.Equal(draftCode, service.GetActiveVersionCode());
    }

    [Fact]
    public async Task CreateDraftVersionAsync_rejects_missing_effective_from()
    {
        await using var db = BuildDb();
        var service = CreateService(db);
        await service.EnsureSeededAsync();

        var result = await service.CreateDraftVersionAsync(
            new CreatePricingCatalogVersionRequest(
                "2026-missing-effective",
                default,
                null,
                CloneFromActive: false),
            "admin-test");

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    [Fact]
    public async Task UpsertEntriesAsync_rejects_non_draft_version()
    {
        await using var db = BuildDb();
        var service = CreateService(db);
        await service.EnsureSeededAsync();

        var result = await service.UpsertEntriesAsync(
            PricingCatalogMetadata.ActiveVersionCode,
            new UpsertPricingCatalogEntriesRequest(
            [
                new PricingCatalogEntryInputDto(
                    "NG",
                    "NGN",
                    "starter",
                    35_000m,
                    0m,
                    true),
            ]),
            "admin-test");

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
    }

    private static PricingCatalogService CreateService(AdeniDbContext db)
    {
        var audit = new InMemoryAuditLogWriter();
        var correlation = new TestCorrelationContext();
        return new PricingCatalogService(db, audit, correlation);
    }

    private static AdeniDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AdeniDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AdeniDbContext(options, new TenantContext());
    }

    private sealed class TestCorrelationContext : ICorrelationContext
    {
        public string CorrelationId { get; } = "test-correlation";

        public void Set(string correlationId) { }
    }
}
