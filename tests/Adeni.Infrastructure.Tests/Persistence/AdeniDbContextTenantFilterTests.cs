namespace Adeni.Infrastructure.Tests.Persistence;

using Adeni.Application.Abstractions;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class AdeniDbContextTenantFilterTests
{
    [Fact]
    public async Task Tenant_filter_returns_only_current_tenant_business_users()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        Seed(scope.ServiceProvider, tenantA, tenantB);

        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.EnableTenantFilter(new TenantId(tenantA));

        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.SyncTenantFilter();
        var users = await db.BusinessUsers.ToListAsync();

        Assert.Single(users);
        Assert.Equal(tenantA, users[0].TenantId);
    }

    [Fact]
    public async Task Disabled_filter_returns_all_business_users()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        Seed(scope.ServiceProvider, tenantA, tenantB);

        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.DisableTenantFilter();

        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.SyncTenantFilter();
        var users = await db.BusinessUsers.ToListAsync();

        Assert.Equal(2, users.Count);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddDbContext<AdeniDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        return services.BuildServiceProvider();
    }

    private static void Seed(IServiceProvider services, Guid tenantA, Guid tenantB)
    {
        var db = services.GetRequiredService<AdeniDbContext>();

        db.Tenants.AddRange(
            new Tenant { Id = tenantA, Name = "Salon A", Status = TenantStatus.Verified, CreatedAt = DateTimeOffset.UtcNow },
            new Tenant { Id = tenantB, Name = "Salon B", Status = TenantStatus.Verified, CreatedAt = DateTimeOffset.UtcNow });

        db.BusinessUsers.AddRange(
            new BusinessUser { Id = Guid.NewGuid(), TenantId = tenantA, Auth0Sub = "auth0|a", CreatedAt = DateTimeOffset.UtcNow },
            new BusinessUser { Id = Guid.NewGuid(), TenantId = tenantB, Auth0Sub = "auth0|b", CreatedAt = DateTimeOffset.UtcNow });

        db.SaveChanges();
    }
}
