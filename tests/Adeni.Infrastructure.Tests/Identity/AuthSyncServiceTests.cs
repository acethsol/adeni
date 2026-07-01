namespace Adeni.Infrastructure.Tests.Identity;

using Adeni.Application.Auth;
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
    public async Task Sync_creates_business_user_with_draft_tenant()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AuthSyncService>();

        var result = await service.SyncAsync(
            new SyncAuthUserRequest("auth0|biz1", "Salon Lagos", null, null, "business"),
            null);

        Assert.True(result.IsSuccess);
        Assert.Equal("business", result.Value!.Role);
        Assert.NotNull(result.Value.TenantId);

        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenant = await db.Tenants.FirstAsync(t => t.Id == result.Value.TenantId);
        Assert.Equal(TenantStatus.Draft, tenant.Status);
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
