namespace Adeni.Infrastructure.Tests.Subscriptions;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class EntitlementsServiceTests
{
    [Fact]
    public async Task Starter_tier_allows_unlimited_bookings()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var entitlements = scope.ServiceProvider.GetRequiredService<IEntitlementsService>();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Starter Test",
            Status = TenantStatus.Verified,
            SubscriptionTier = SubscriptionTier.Free,
            SubscriptionSku = SubscriptionSku.Starter,
            BillingStatus = TenantBillingStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Tenants.Add(tenant);

        for (var i = 0; i < SubscriptionEntitlements.FreeMonthlyBookingLimit + 5; i++)
        {
            db.Bookings.Add(new BookingRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ServiceOfferingId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                StartAt = DateTimeOffset.UtcNow.AddDays(i + 1),
                EndAt = DateTimeOffset.UtcNow.AddDays(i + 1).AddHours(1),
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync();

        var result = await entitlements.EnsureCanCreateBookingForTenantAsync(tenant);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Trialing_professional_entitles_messaging()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<IEntitlementsService>();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Trial Pro",
            Status = TenantStatus.Verified,
            SubscriptionSku = SubscriptionSku.Starter,
            BillingStatus = TenantBillingStatus.Trialing,
            TrialSku = SubscriptionSku.Professional,
            TrialEndsAt = DateTimeOffset.UtcNow.AddDays(14),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var result = await entitlements.EnsureCanUseMessagingForTenantAsync(tenant);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Active_starter_blocks_messaging()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<IEntitlementsService>();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Starter Only",
            Status = TenantStatus.Verified,
            SubscriptionSku = SubscriptionSku.Starter,
            BillingStatus = TenantBillingStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var result = await entitlements.EnsureCanUseMessagingForTenantAsync(tenant);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.MessagingNotEntitled, result.Error.Code);
    }

    [Fact]
    public async Task Free_tier_blocks_second_location()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<IEntitlementsService>();

        var result = await entitlements.EnsureCanAddLocationAsync(
            Guid.NewGuid(),
            SubscriptionTier.Free,
            activeLocationCount: 1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.MultiLocationRequired, result.Error.Code);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Adeni.Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddScoped<IEntitlementsService, EntitlementsService>();
        return services.BuildServiceProvider();
    }
}
