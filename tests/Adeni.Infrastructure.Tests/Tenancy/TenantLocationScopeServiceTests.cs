namespace Adeni.Infrastructure.Tests.Tenancy;

using Adeni.Application.Abstractions;
using Adeni.Application.Subscriptions;
using Adeni.Application.Tenancy;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Tenancy;
using Adeni.Infrastructure.Tests.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class TenantLocationScopeServiceTests
{
    [Theory]
    [InlineData(SubscriptionSku.Starter, false)]
    [InlineData(SubscriptionSku.Business, true)]
    public async Task ResolveListLocationAsync_PreservesEntitlementAndExplicitBranchScope(SubscriptionSku sku, bool allBranches)
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ITenantLocationScopeService>();
        var tenantId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var siblingId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, SubscriptionSku = sku, BillingStatus = TenantBillingStatus.Active });
        db.BusinessLocations.AddRange(
            new BusinessLocation { Id = primaryId, TenantId = tenantId, IsPrimary = true },
            new BusinessLocation { Id = siblingId, TenantId = tenantId });
        await db.SaveChangesAsync();

        var result = await service.ResolveListLocationAsync(tenantId, null);
        Assert.True(result.IsSuccess);
        Assert.Equal(allBranches ? (Guid?)null : primaryId, result.Value);
        Assert.Equal(siblingId, (await service.ResolveListLocationAsync(tenantId, siblingId)).Value);
        Assert.Equal(primaryId, (await service.ResolveAvailabilityLocationAsync(tenantId, null)).Value);
    }

    [Fact]
    public async Task LocationQueries_PreserveMissingBusinessAndInvalidLocationErrors()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ITenantLocationScopeService>();
        var tenantId = Guid.NewGuid();
        var inactive = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        db.BusinessLocations.AddRange(
            new BusinessLocation { Id = inactive, TenantId = tenantId, IsActive = false },
            new BusinessLocation { Id = foreign, TenantId = Guid.NewGuid() });
        await db.SaveChangesAsync();
        var missing = await service.ResolveListLocationAsync(tenantId, null);
        Assert.Equal("not_found", missing.Error.Code);
        Assert.Equal("Business was not found.", missing.Error.Message);
        Assert.True((await service.ValidateOptionalLocationIdAsync(tenantId, null)).IsSuccess);
        foreach (var id in new[] { inactive, foreign, Guid.NewGuid() })
        {
            var result = await service.ValidateOptionalLocationIdAsync(tenantId, id);
            Assert.Equal("validation", result.Error.Code);
            Assert.Equal("Unknown or inactive location for this business.", result.Error.Message);
        }

        Assert.Equal("Business location was not found.", (await service.ResolveAvailabilityLocationAsync(tenantId, null)).Error.Message);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddEntitlementsModule();
        services.AddScoped<ITenantLocationScopeService, TenantLocationScopeService>();
        return services.BuildServiceProvider();
    }
}
