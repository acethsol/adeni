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

        var tenant = CreateTenant(SubscriptionSku.Starter, TenantBillingStatus.Active);
        db.Tenants.Add(tenant);

        for (var i = 0; i < 30; i++)
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
    public void Starter_seed_includes_messaging()
    {
        var starter = SubscriptionEntitlements.ForSku(SubscriptionSku.Starter);
        Assert.True(starter.Messaging);
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
    public async Task Active_starter_allows_messaging()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<IEntitlementsService>();

        var tenant = CreateTenant(SubscriptionSku.Starter, TenantBillingStatus.Active);
        var result = await entitlements.EnsureCanUseMessagingForTenantAsync(tenant);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Starter_blocks_second_location()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<IEntitlementsService>();

        var tenant = CreateTenant(SubscriptionSku.Starter, TenantBillingStatus.Active);
        var result = await entitlements.EnsureCanAddLocationForTenantAsync(tenant, activeLocationCount: 1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.LocationLimitReached, result.Error.Code);
    }

    [Fact]
    public async Task Starter_blocks_fourth_practitioner()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<IEntitlementsService>();

        var tenant = CreateTenant(SubscriptionSku.Starter, TenantBillingStatus.Active);
        var result = await entitlements.EnsureCanAddPractitionerForTenantAsync(tenant, activePractitionerCount: 3);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.PractitionerLimitReached, result.Error.Code);
    }

    [Fact]
    public async Task Business_allows_tenth_practitioner()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<IEntitlementsService>();

        var tenant = CreateTenant(SubscriptionSku.Business, TenantBillingStatus.Active);
        var result = await entitlements.EnsureCanAddPractitionerForTenantAsync(tenant, activePractitionerCount: 10);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Business_blocks_fourth_location()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<IEntitlementsService>();

        var tenant = CreateTenant(SubscriptionSku.Business, TenantBillingStatus.Active);
        var result = await entitlements.EnsureCanAddLocationForTenantAsync(tenant, activeLocationCount: 3);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.LocationLimitReached, result.Error.Code);
    }

    [Fact]
    public async Task Business_blocks_26th_practitioner()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<IEntitlementsService>();

        var tenant = CreateTenant(SubscriptionSku.Business, TenantBillingStatus.Active);
        var result = await entitlements.EnsureCanAddPractitionerForTenantAsync(tenant, activePractitionerCount: 25);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.PractitionerLimitReached, result.Error.Code);
    }

    [Fact]
    public void Business_seed_caps_locations_and_practitioners()
    {
        var business = SubscriptionEntitlements.ForSku(SubscriptionSku.Business);
        Assert.Equal(3, business.MaxLocations);
        Assert.Equal(25, business.MaxPractitioners);
        Assert.True(business.AdditionalLocationsAllowed);
        Assert.True(business.AdditionalPractitionersAllowed);
    }

    [Fact]
    public async Task Starter_blocks_waitlist()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<IEntitlementsService>();

        var tenant = CreateTenant(SubscriptionSku.Starter, TenantBillingStatus.Active);
        var result = await entitlements.EnsureCanUseWaitlistForTenantAsync(tenant);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.WaitlistNotEntitled, result.Error.Code);
    }

    [Fact]
    public async Task Professional_allows_waitlist()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var entitlements = scope.ServiceProvider.GetRequiredService<IEntitlementsService>();

        var tenant = CreateTenant(SubscriptionSku.Professional, TenantBillingStatus.Active);
        var result = await entitlements.EnsureCanUseWaitlistForTenantAsync(tenant);
        Assert.True(result.IsSuccess);
    }

    private static Tenant CreateTenant(SubscriptionSku sku, TenantBillingStatus billingStatus) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Entitlements Test",
            Status = TenantStatus.Verified,
            SubscriptionSku = sku,
            BillingStatus = billingStatus,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Adeni.Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddEntitlementsModule();
        return services.BuildServiceProvider();
    }
}
