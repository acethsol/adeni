namespace Adeni.Infrastructure.Tests.Admin;

using Adeni.Domain.Finance;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Admin;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class AdminTransactionSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_emptyQuery_listsRecent_withKindFilterAndPaging()
    {
        await using var db = CreateDb();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        db.Tenants.AddRange(
            new Tenant { Id = tenantA, Name = "Alpha Salon", Status = TenantStatus.Verified, CreatedAt = DateTimeOffset.UtcNow },
            new Tenant { Id = tenantB, Name = "Beta Barbers", Status = TenantStatus.Verified, CreatedAt = DateTimeOffset.UtcNow });

        for (var i = 0; i < 12; i++)
        {
            db.PaymentIntents.Add(new PaymentIntentRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA,
                Amount = 1000m + i,
                PlatformFeeAmount = 50m,
                Currency = "NGN",
                Status = PaymentIntentStatus.Completed,
                Type = PaymentIntentType.Link,
                ProviderReference = $"devseed_commerce_{i:000}",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-i),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-i),
            });
        }

        for (var i = 0; i < 5; i++)
        {
            db.TenantSubscriptionCharges.Add(new TenantSubscriptionCharge
            {
                Id = Guid.NewGuid(),
                TenantId = tenantB,
                IdempotencyKey = $"saas-{i}",
                ProviderReference = $"devseed_saas_{i:000}",
                EventType = "subscription.charge.success",
                SubscriptionSku = SubscriptionSku.Professional,
                AmountMinor = 250000,
                Currency = "NGN",
                Status = "success",
                OccurredAt = DateTimeOffset.UtcNow.AddDays(-i),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-i),
            });
        }

        await db.SaveChangesAsync();

        var service = new AdminTransactionSearchService(db);

        var allPage1 = await service.SearchAsync(null, "all", page: 1, pageSize: 10);
        Assert.True(allPage1.IsSuccess);
        Assert.Equal(17, allPage1.Value!.TotalCount);
        Assert.Equal(10, allPage1.Value.Items.Count);

        var allPage2 = await service.SearchAsync(null, "all", page: 2, pageSize: 10);
        Assert.True(allPage2.IsSuccess);
        Assert.Equal(7, allPage2.Value!.Items.Count);

        var saasOnly = await service.SearchAsync(null, "saas", page: 1, pageSize: 10);
        Assert.True(saasOnly.IsSuccess);
        Assert.Equal(5, saasOnly.Value!.TotalCount);
        Assert.All(saasOnly.Value.Items, item => Assert.Equal("saas", item.Kind));
    }

    [Fact]
    public async Task SearchAsync_query_matchesProviderReference()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Gamma Nails",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.PaymentIntents.Add(new PaymentIntentRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Amount = 4200m,
            PlatformFeeAmount = 210m,
            Currency = "NGN",
            Status = PaymentIntentStatus.Completed,
            Type = PaymentIntentType.Link,
            ProviderReference = "paystack_unique_ref_99",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = new AdminTransactionSearchService(db);
        var result = await service.SearchAsync("unique_ref", "all", 1, 10);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal("commerce", result.Value.Items[0].Kind);
        Assert.Equal("Gamma Nails", result.Value.Items[0].TenantName);
    }

    [Fact]
    public async Task SearchAsync_singleCharacterQuery_failsValidation()
    {
        await using var db = CreateDb();
        var service = new AdminTransactionSearchService(db);

        var result = await service.SearchAsync("a", "all", 1, 10);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    private static AdeniDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AdeniDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.DisableTenantFilter();
        return new AdeniDbContext(options, tenantContext);
    }
}
