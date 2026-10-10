namespace Adeni.Infrastructure.Tests.Identity;

using Adeni.Application.Auth;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class AuthSyncServiceTests
{
    [Fact]
    public async Task Sync_creates_customer_profile()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AuthSyncService>();

        var result = await service.SyncAsync(
            new SyncAuthUserRequest("auth0|cust1", "Ada", "ada@example.com", "+2348012345678", "customer"),
            null);

        Assert.True(result.IsSuccess);
        Assert.Equal("customer", result.Value!.Role);
        Assert.Equal("auth0|cust1", result.Value.Auth0Sub);
    }

    [Fact]
    public async Task Sync_business_without_membership_does_not_create_tenant()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AuthSyncService>();

        var result = await service.SyncAsync(
            new SyncAuthUserRequest("auth0|biz1", "Salon Lagos", null, null, "business"),
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.BusinessAccessDenied, result.Error.Code);

        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        Assert.Empty(await db.Tenants.ToListAsync());
        Assert.Empty(await db.BusinessUsers.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Sync_business_returns_existing_membership()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Lekki Cuts",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessUsers.Add(new BusinessUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Auth0Sub = "auth0|biz-existing",
            Role = PortalPermissionRoles.Practitioner,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<AuthSyncService>();
        var result = await service.SyncAsync(
            new SyncAuthUserRequest("auth0|biz-existing", "Fela", "fela@example.com", null, "business"),
            null);

        Assert.True(result.IsSuccess);
        Assert.Equal(tenantId, result.Value!.TenantId);
        Assert.Equal(1, await db.Tenants.CountAsync());
    }

    [Fact]
    public async Task Sync_rejects_mismatched_token_subject()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AuthSyncService>();

        var result = await service.SyncAsync(
            new SyncAuthUserRequest("auth0|a", "A", null, null, "customer"),
            "auth0|b");

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<AuthSyncService>();
        return services.BuildServiceProvider();
    }
}
