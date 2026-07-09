namespace Adeni.Infrastructure.Tests.Persistence;

using Adeni.Application.Abstractions;
using Adeni.Domain.Booking;
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

        SeedBusinessUsers(scope.ServiceProvider, tenantA, tenantB);

        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.EnableTenantFilter(new TenantId(tenantA));

        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.SyncTenantFilter();
        var users = await db.BusinessUsers.ToListAsync();

        Assert.Single(users);
        Assert.Equal(tenantA, users[0].TenantId);
    }

    [Fact]
    public async Task Tenant_filter_returns_only_current_tenant_services()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        SeedServices(scope.ServiceProvider, tenantA, tenantB);

        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.EnableTenantFilter(new TenantId(tenantA));

        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.SyncTenantFilter();
        var services = await db.ServiceOfferings.ToListAsync();

        Assert.Single(services);
        Assert.Equal(tenantA, services[0].TenantId);
    }

    [Fact]
    public async Task Tenant_filter_returns_only_current_tenant_bookings()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        SeedBookings(scope.ServiceProvider, tenantA, tenantB);

        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.EnableTenantFilter(new TenantId(tenantA));

        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.SyncTenantFilter();
        var bookings = await db.Bookings.ToListAsync();

        Assert.Single(bookings);
        Assert.Equal(tenantA, bookings[0].TenantId);
    }

    [Fact]
    public async Task Tenant_filter_returns_only_current_tenant_locations()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        SeedLocations(scope.ServiceProvider, tenantA, tenantB);

        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.EnableTenantFilter(new TenantId(tenantA));

        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.SyncTenantFilter();
        var locations = await db.BusinessLocations.ToListAsync();

        Assert.Single(locations);
        Assert.Equal(tenantA, locations[0].TenantId);
    }

    [Fact]
    public async Task Disabled_filter_returns_all_business_users()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        SeedBusinessUsers(scope.ServiceProvider, tenantA, tenantB);

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

    private static void SeedBusinessUsers(IServiceProvider services, Guid tenantA, Guid tenantB)
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

    private static void SeedServices(IServiceProvider services, Guid tenantA, Guid tenantB)
    {
        var db = services.GetRequiredService<AdeniDbContext>();
        var now = DateTimeOffset.UtcNow;

        db.Tenants.AddRange(
            new Tenant { Id = tenantA, Name = "Salon A", Status = TenantStatus.Verified, CreatedAt = now },
            new Tenant { Id = tenantB, Name = "Salon B", Status = TenantStatus.Verified, CreatedAt = now });

        db.ServiceOfferings.AddRange(
            new ServiceOffering { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Cut A", DurationMinutes = 30, PriceAmount = 10, Currency = "NGN", IsActive = true, CreatedAt = now },
            new ServiceOffering { Id = Guid.NewGuid(), TenantId = tenantB, Name = "Cut B", DurationMinutes = 30, PriceAmount = 10, Currency = "NGN", IsActive = true, CreatedAt = now });

        db.SaveChanges();
    }

    private static void SeedBookings(IServiceProvider services, Guid tenantA, Guid tenantB)
    {
        var db = services.GetRequiredService<AdeniDbContext>();
        var now = DateTimeOffset.UtcNow;
        var serviceA = Guid.NewGuid();
        var serviceB = Guid.NewGuid();

        db.Tenants.AddRange(
            new Tenant { Id = tenantA, Name = "Salon A", Status = TenantStatus.Verified, CreatedAt = now },
            new Tenant { Id = tenantB, Name = "Salon B", Status = TenantStatus.Verified, CreatedAt = now });

        db.ServiceOfferings.AddRange(
            new ServiceOffering { Id = serviceA, TenantId = tenantA, Name = "Cut A", DurationMinutes = 30, PriceAmount = 10, Currency = "NGN", IsActive = true, CreatedAt = now },
            new ServiceOffering { Id = serviceB, TenantId = tenantB, Name = "Cut B", DurationMinutes = 30, PriceAmount = 10, Currency = "NGN", IsActive = true, CreatedAt = now });

        db.Bookings.AddRange(
            new BookingRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA,
                ServiceOfferingId = serviceA,
                CustomerId = Guid.NewGuid(),
                StartAt = now.AddDays(1),
                EndAt = now.AddDays(1).AddMinutes(30),
                Status = BookingStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            },
            new BookingRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantB,
                ServiceOfferingId = serviceB,
                CustomerId = Guid.NewGuid(),
                StartAt = now.AddDays(1),
                EndAt = now.AddDays(1).AddMinutes(30),
                Status = BookingStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            });

        db.SaveChanges();
    }

    private static void SeedLocations(IServiceProvider services, Guid tenantA, Guid tenantB)
    {
        var db = services.GetRequiredService<AdeniDbContext>();
        var now = DateTimeOffset.UtcNow;

        db.Tenants.AddRange(
            new Tenant { Id = tenantA, Name = "Salon A", Status = TenantStatus.Verified, CreatedAt = now },
            new Tenant { Id = tenantB, Name = "Salon B", Status = TenantStatus.Verified, CreatedAt = now });

        db.BusinessProfiles.AddRange(
            new BusinessProfile { TenantId = tenantA, CategorySlug = "barbers", Phone = "+2348012345678", UpdatedAt = now },
            new BusinessProfile { TenantId = tenantB, CategorySlug = "barbers", Phone = "+2348012345678", UpdatedAt = now });

        db.BusinessLocations.AddRange(
            new BusinessLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantA,
                Slug = "salon-a",
                Name = "A",
                MarketId = "lagos",
                AddressLine = "A",
                Area = "A",
                IsPrimary = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new BusinessLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantB,
                Slug = "salon-b",
                Name = "B",
                MarketId = "lagos",
                AddressLine = "B",
                Area = "B",
                IsPrimary = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });

        db.SaveChanges();
    }
}
