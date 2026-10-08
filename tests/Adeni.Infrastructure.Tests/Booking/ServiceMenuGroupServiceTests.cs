namespace Adeni.Infrastructure.Tests.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Caching;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Tests.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class ServiceMenuGroupServiceTests
{
    [Fact]
    public async Task CreateAssignDelete_ClearsServiceFk()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);

        var groups = scope.ServiceProvider.GetRequiredService<IServiceMenuGroupService>();
        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();

        var created = await groups.CreateAsync(tenantId, new CreateServiceMenuGroupRequest("Hair", 0));
        Assert.True(created.IsSuccess);

        var service = await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest(
                "Cut",
                null,
                5000m,
                "NGN",
                30,
                MenuGroupId: created.Value!.Id,
                SortOrder: 1));
        Assert.True(service.IsSuccess);
        Assert.Equal(created.Value.Id, service.Value!.MenuGroupId);

        var deleted = await groups.DeleteAsync(tenantId, created.Value.Id);
        Assert.True(deleted.IsSuccess);

        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var offering = await db.ServiceOfferings.AsNoTracking().SingleAsync(x => x.Id == service.Value.Id);
        Assert.Null(offering.MenuGroupId);
    }

    [Fact]
    public async Task ListForTenant_OrdersByGroupThenServiceSort()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);

        var groups = scope.ServiceProvider.GetRequiredService<IServiceMenuGroupService>();
        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();

        var hair = await groups.CreateAsync(tenantId, new CreateServiceMenuGroupRequest("Hair", 0));
        var nails = await groups.CreateAsync(tenantId, new CreateServiceMenuGroupRequest("Nails", 1));
        Assert.True(hair.IsSuccess);
        Assert.True(nails.IsSuccess);

        await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Gel", null, 8000m, "NGN", 45, MenuGroupId: nails.Value!.Id, SortOrder: 0));
        await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Fade", null, 5000m, "NGN", 30, MenuGroupId: hair.Value!.Id, SortOrder: 1));
        await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Cut", null, 4000m, "NGN", 30, MenuGroupId: hair.Value.Id, SortOrder: 0));

        var list = await catalog.ListForTenantAsync(tenantId);
        Assert.Equal(["Cut", "Fade", "Gel"], list.Items.Select(x => x.Name).ToArray());
        Assert.Equal(2, list.Groups.Count);
    }

    private static async Task<Guid> SeedVerifiedTenantAsync(IServiceProvider provider)
    {
        var db = provider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Menu Test Shop",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
            VerifiedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "hair-grooming",
            Phone = "+2348012345678",
            Description = "Menu tests",
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
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddCategoryWorkflowCatalog();
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IServiceMenuGroupService, ServiceMenuGroupService>();
        return services.BuildServiceProvider();
    }
}
