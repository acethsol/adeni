namespace Adeni.Infrastructure.Tests.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Caching;
using Adeni.Application.Markets;
using Adeni.Domain.Booking;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public sealed class AvailabilityLocationScopeTests
{
    [Fact]
    public async Task Slots_by_slug_use_branch_hours_not_sibling_branch()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var availability = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();

        var tenantId = Guid.NewGuid();
        var earlyBranchId = Guid.NewGuid();
        var lateBranchId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Two Branch Shop",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
            VerifiedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "barbers",
            Phone = "+2348012345678",
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessLocations.AddRange(
            new BusinessLocation
            {
                Id = earlyBranchId,
                TenantId = tenantId,
                Slug = "early-branch",
                Name = "Early",
                MarketId = "lagos",
                AddressLine = "1 Road",
                Area = "A",
                IsPrimary = true,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
            new BusinessLocation
            {
                Id = lateBranchId,
                TenantId = tenantId,
                Slug = "late-branch",
                Name = "Late",
                MarketId = "lagos",
                AddressLine = "2 Road",
                Area = "B",
                IsPrimary = false,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        var serviceId = Guid.NewGuid();
        db.ServiceOfferings.Add(new ServiceOffering
        {
            Id = serviceId,
            TenantId = tenantId,
            Name = "Cut",
            PriceAmount = 5000m,
            Currency = "NGN",
            DurationMinutes = 30,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        await availability.ReplaceWeeklyRulesAsync(
            tenantId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0))],
            earlyBranchId,
            CancellationToken.None);
        await availability.ReplaceWeeklyRulesAsync(
            tenantId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(17, 0))],
            lateBranchId,
            CancellationToken.None);

        var monday = NextMondayUtc();
        var rangeStart = monday.AddHours(8);
        var rangeEnd = monday.AddHours(18);

        var earlySlots = await availability.GetAvailableSlotsBySlugAsync(
            "early-branch",
            serviceId,
            rangeStart,
            rangeEnd,
            cancellationToken: CancellationToken.None);
        var lateSlots = await availability.GetAvailableSlotsBySlugAsync(
            "late-branch",
            serviceId,
            rangeStart,
            rangeEnd,
            cancellationToken: CancellationToken.None);

        Assert.True(earlySlots.IsSuccess);
        Assert.True(lateSlots.IsSuccess);
        Assert.NotEmpty(earlySlots.Value!);
        Assert.NotEmpty(lateSlots.Value!);
        Assert.InRange(earlySlots.Value![0].StartAt.UtcDateTime.Hour, 9, 11);
        Assert.InRange(lateSlots.Value![0].StartAt.UtcDateTime.Hour, 14, 16);
        Assert.NotEqual(
            earlySlots.Value![0].StartAt.UtcDateTime.Hour,
            lateSlots.Value![0].StartAt.UtcDateTime.Hour);
    }

    private static DateTimeOffset NextMondayUtc()
    {
        var today = DateTimeOffset.UtcNow;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0)
        {
            daysUntilMonday = 7;
        }

        return new DateTimeOffset(today.Date.AddDays(daysUntilMonday), TimeSpan.Zero);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddSingleton<ICacheService, DistributedCacheService>();
        services.AddSingleton<IDistributedLockProvider, NoOpLockProvider>();
        services.Configure<MarketOptions>(options => options.DefaultTimeZoneId = "UTC");
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddScoped<ITenantSchedulingTimeZone, TenantSchedulingTimeZone>();
        services.AddScoped<IBusinessClosedDateService, BusinessClosedDateService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        return services.BuildServiceProvider();
    }
}
