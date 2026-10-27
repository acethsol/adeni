namespace Adeni.Infrastructure.Tests.Trust;

using Adeni.Application.Realtime;
using Adeni.Application.Trust;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Tests.Realtime;
using Adeni.Infrastructure.Trust;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class VerificationBadgeServiceRealtimeTests
{
    [Fact]
    public async Task RequestBadge_publishes_pending_badge_and_admin_count()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var (tenantId, ownerSub) = await SeedVerifiedOwnerAsync(scope);
        var service = scope.ServiceProvider.GetRequiredService<VerificationBadgeService>();

        var result = await service.RequestBadgeAsync(
            tenantId,
            ownerSub,
            new RequestVerificationBadgeRequest(VerificationBadgeType.Cac, "RC1234567"));

        Assert.True(result.IsSuccess);
        var publisher = provider.GetRequiredService<RecordingTenantRealtimePublisher>();
        Assert.Equal([(tenantId, (string?)"cac", "pending")], publisher.TenantBadges);
        Assert.Equal([1], publisher.AdminBadgeCounts);
    }

    [Fact]
    public async Task GrantBadge_publishes_granted_badge_and_decremented_admin_count()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var (tenantId, ownerSub) = await SeedVerifiedOwnerAsync(scope);
        var service = scope.ServiceProvider.GetRequiredService<VerificationBadgeService>();
        await service.RequestBadgeAsync(
            tenantId,
            ownerSub,
            new RequestVerificationBadgeRequest(VerificationBadgeType.Cac, "RC1234567"));
        var publisher = provider.GetRequiredService<RecordingTenantRealtimePublisher>();
        publisher.TenantBadges.Clear();
        publisher.AdminBadgeCounts.Clear();

        var result = await service.GrantBadgeAsync(tenantId, VerificationBadgeType.Cac, "admin-1");

        Assert.True(result.IsSuccess);
        Assert.Equal([(tenantId, (string?)"cac", "granted")], publisher.TenantBadges);
        Assert.Equal([0], publisher.AdminBadgeCounts);
    }

    [Fact]
    public async Task RevokeBadge_publishes_revoked_badge_and_decremented_admin_count()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var (tenantId, ownerSub) = await SeedVerifiedOwnerAsync(scope);
        var service = scope.ServiceProvider.GetRequiredService<VerificationBadgeService>();
        await service.RequestBadgeAsync(
            tenantId,
            ownerSub,
            new RequestVerificationBadgeRequest(VerificationBadgeType.License, null));
        var publisher = provider.GetRequiredService<RecordingTenantRealtimePublisher>();
        publisher.TenantBadges.Clear();
        publisher.AdminBadgeCounts.Clear();

        var result = await service.RevokeBadgeAsync(tenantId, VerificationBadgeType.License, "admin-1");

        Assert.True(result.IsSuccess);
        Assert.Equal([(tenantId, (string?)"license", "revoked")], publisher.TenantBadges);
        Assert.Equal([0], publisher.AdminBadgeCounts);
    }

    private static async Task<(Guid TenantId, string OwnerSub)> SeedVerifiedOwnerAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        var ownerSub = $"auth0|owner-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Badge Salon",
            Status = TenantStatus.Verified,
            CreatedAt = now,
            VerifiedAt = now,
        });
        db.BusinessUsers.Add(new BusinessUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Auth0Sub = ownerSub,
            Role = "owner",
            CreatedAt = now,
        });
        await db.SaveChangesAsync();
        return (tenantId, ownerSub);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddSingleton<Application.Abstractions.ICorrelationContext, CorrelationContext>();
        services.AddSingleton<Application.Abstractions.IAuditLogWriter, InMemoryAuditLogWriter>();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton<RecordingTenantRealtimePublisher>();
        services.AddSingleton<ITenantRealtimePublisher>(sp => sp.GetRequiredService<RecordingTenantRealtimePublisher>());
        services.AddScoped<VerificationBadgeService>();
        return services.BuildServiceProvider();
    }
}
