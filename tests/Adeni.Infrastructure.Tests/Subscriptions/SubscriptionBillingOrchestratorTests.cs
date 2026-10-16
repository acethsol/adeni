namespace Adeni.Infrastructure.Tests.Subscriptions;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Identity;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class SubscriptionBillingOrchestratorTests
{
    [Fact]
    public async Task ApplyWebhookEventAsync_activates_tenant_and_is_idempotent()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ISubscriptionBillingOrchestrator>();
        var (tenantId, _) = await SeedTenantWithUserAsync(db);

        var webhook = new SubscriptionWebhookEvent(
            "subscription.create",
            "stub_sub_test_pro",
            tenantId,
            "pro",
            "active",
            DateTimeOffset.UtcNow);

        var first = await orchestrator.ApplyWebhookEventAsync(webhook);
        var second = await orchestrator.ApplyWebhookEventAsync(webhook);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Id, second.Value!.Id);

        var tenant = await db.Tenants.AsNoTracking().SingleAsync(x => x.Id == tenantId);
        Assert.Equal(SubscriptionSku.Professional, tenant.SubscriptionSku);
        Assert.Equal(TenantBillingStatus.Active, tenant.BillingStatus);
        Assert.Equal(1, await db.TenantSubscriptionCharges.CountAsync(x => x.TenantId == tenantId));
    }

    [Fact]
    public async Task CompleteStubCheckoutAsync_rejects_cross_tenant_reference()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ISubscriptionBillingOrchestrator>();
        var (tenantId, auth0Sub) = await SeedTenantWithUserAsync(db);
        var (otherTenantId, _) = await SeedTenantWithUserAsync(db);

        var reference = $"stub_sub_{otherTenantId:N}_pro";
        var result = await orchestrator.CompleteStubCheckoutAsync(tenantId, auth0Sub, reference);

        Assert.True(result.IsFailure);
    }

    private static async Task<(Guid TenantId, string Auth0Sub)> SeedTenantWithUserAsync(AdeniDbContext db)
    {
        var tenantId = Guid.NewGuid();
        var auth0Sub = $"auth0|{tenantId:N}";
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Billing test",
            Status = TenantStatus.Verified,
            SubscriptionSku = SubscriptionSku.Starter,
            BillingStatus = TenantBillingStatus.Trialing,
            CreatedAt = DateTimeOffset.UtcNow,
            VerifiedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessUsers.Add(new BusinessUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Auth0Sub = auth0Sub,
            Role = "owner",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return (tenantId, auth0Sub);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddScoped<ISubscriptionBillingOrchestrator, SubscriptionBillingOrchestrator>();
        services.AddLogging();
        return services.BuildServiceProvider();
    }
}
