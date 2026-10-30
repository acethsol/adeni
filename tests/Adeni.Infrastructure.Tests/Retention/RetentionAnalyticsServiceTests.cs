namespace Adeni.Infrastructure.Tests.Retention;

using Adeni.Application.Retention;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Retention;
using Adeni.Infrastructure.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

public sealed class RetentionAnalyticsServiceTests
{
    private sealed class ParityHost : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Adeni.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    static RetentionAnalyticsServiceTests()
    {
        _ = new JsonPlanEntitlementsMatrix(new ParityHost());
    }

    [Fact]
    public async Task GetMembershipMrr_WithoutAdvancedAnalytics_IsDenied()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDb();
        db.Add(new Tenant
        {
            Id = tenantId,
            Name = "Salon",
            SubscriptionSku = SubscriptionSku.Starter,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var entitlements = new SkuEntitlements(SubscriptionSku.Starter);
        var guard = new RetentionEntitlementGuard(db, entitlements);
        var service = new RetentionAnalyticsService(
            db,
            guard,
            new MembershipMrrService(db, guard, TimeProvider.System),
            TimeProvider.System);

        var result = await service.GetMembershipMrrAsync(tenantId);

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.AdvancedRetentionAnalyticsNotEntitled, result.Error!.Code);
    }

    [Fact]
    public async Task GetSummary_CountsPackageRedemptionsInRange()
    {
        var tenantId = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        await using var db = NewDb();
        db.Add(new Tenant
        {
            Id = tenantId,
            Name = "Pro Salon",
            SubscriptionSku = SubscriptionSku.Professional,
            CreatedAt = at,
        });
        db.Add(new BenefitTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BenefitBalanceId = Guid.NewGuid(),
            Kind = BenefitTransactionKind.Redeem,
            Quantity = 2,
            OccurredAt = at.AddDays(1),
            Actor = "staff",
        });
        await db.SaveChangesAsync();

        var entitlements = new SkuEntitlements(SubscriptionSku.Professional);
        var guard = new RetentionEntitlementGuard(db, entitlements);
        var service = new RetentionAnalyticsService(
            db,
            guard,
            new MembershipMrrService(db, guard, TimeProvider.System),
            TimeProvider.System);

        var summary = await service.GetSummaryAsync(
            tenantId,
            new RetentionAnalyticsQuery(at, at.AddDays(7), null));

        Assert.True(summary.IsSuccess, summary.Error?.Code);
        Assert.Equal(2, summary.Value!.Packages.Redemptions);
    }

    private static AdeniDbContext NewDb() =>
        new(
            new DbContextOptionsBuilder<AdeniDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new TenantContext());

    private sealed class SkuEntitlements(SubscriptionSku sku) : IEntitlementsService
    {
        public TenantEntitlements GetEntitlementsForTenant(Tenant tenant) =>
            PlanEntitlementsCatalog.ForSku(sku);

        public Task<Result<TenantEntitlements>> GetEntitlementsForMemberAsync(
            Guid tenantId,
            string auth0Sub,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(PlanEntitlementsCatalog.ForSku(sku)));

        public Task<int> GetBookingsUsedThisMonthAsync(
            Guid tenantId,
            Guid? locationId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<int> GetActiveLocationCountAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        public Task<int> GetActivePractitionerCountAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        public Task<Result> EnsureCanCreateBookingForTenantAsync(Tenant tenant, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> EnsureCanAddLocationForTenantAsync(
            Tenant tenant,
            int activeLocationCount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> EnsureCanAddPractitionerForTenantAsync(
            Tenant tenant,
            int activePractitionerCount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> EnsureCanUseMessagingForTenantAsync(Tenant tenant, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> EnsureCanUseWaitlistForTenantAsync(Tenant tenant, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<SubscriptionUsageResponse> GetUsageForTenantAsync(
            Tenant tenant,
            Guid? bookingsLocationId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SubscriptionUsageResponse(
                sku.ToString(),
                PlanEntitlementsCatalog.ForSku(sku),
                0,
                null,
                1,
                1));
    }
}
