namespace Adeni.Infrastructure.Tests.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Caching;
using Adeni.Application.Common;
using Adeni.Application.Events;
using Adeni.Application.Markets;
using Adeni.Application.Reviews;
using Adeni.Application.Storage;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Reviews;
using Adeni.Infrastructure.Subscriptions;
using Adeni.Infrastructure.Tests.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class PractitionerBookingScopeTests
{
    [Fact]
    public async Task ListForTenant_filters_by_staff_member()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);
        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var availability = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        var staff = scope.ServiceProvider.GetRequiredService<IStaffService>();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var service = await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Cut", null, 5000m, "NGN", 30));
        Assert.True(service.IsSuccess);

        await availability.ReplaceWeeklyRulesAsync(
            tenantId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))]);

        var staffA = await staff.CreateAsync(
            tenantId,
            new CreateStaffMemberRequest("Ada", "A", ServiceOfferingIds: [service.Value!.Id]));
        var staffB = await staff.CreateAsync(
            tenantId,
            new CreateStaffMemberRequest("Baba", "B", ServiceOfferingIds: [service.Value.Id]));
        Assert.True(staffA.IsSuccess);
        Assert.True(staffB.IsSuccess);

        var monday = NextMondayAt(new TimeOnly(10, 0));
        var bookingA = await bookings.CreateAsync(
            "auth0|cust-a",
            new CreateBookingRequest(
                tenantId,
                service.Value.Id,
                monday,
                null,
                StaffMemberId: staffA.Value!.Id));
        var bookingB = await bookings.CreateAsync(
            "auth0|cust-b",
            new CreateBookingRequest(
                tenantId,
                service.Value.Id,
                monday.AddHours(1),
                null,
                StaffMemberId: staffB.Value!.Id));
        Assert.True(bookingA.IsSuccess);
        Assert.True(bookingB.IsSuccess);

        var all = await bookings.ListForTenantAsync(tenantId);
        Assert.Equal(2, all.Count);

        var scoped = await bookings.ListForTenantAsync(tenantId, staffA.Value.Id);
        Assert.Single(scoped);
        Assert.Equal(bookingA.Value!.Id, scoped[0].Id);
    }

    [Fact]
    public async Task Accept_denies_when_staff_scope_does_not_match()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);
        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var availability = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        var staff = scope.ServiceProvider.GetRequiredService<IStaffService>();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var service = await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Cut", null, 5000m, "NGN", 30));
        await availability.ReplaceWeeklyRulesAsync(
            tenantId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))]);

        var staffA = await staff.CreateAsync(
            tenantId,
            new CreateStaffMemberRequest("Ada", "A", ServiceOfferingIds: [service.Value!.Id]));
        var staffB = await staff.CreateAsync(
            tenantId,
            new CreateStaffMemberRequest("Baba", "B", ServiceOfferingIds: [service.Value.Id]));
        Assert.True(staffA.IsSuccess);
        Assert.True(staffB.IsSuccess);

        var created = await bookings.CreateAsync(
            "auth0|cust-a",
            new CreateBookingRequest(
                tenantId,
                service.Value.Id,
                NextMondayAt(new TimeOnly(10, 0)),
                null,
                StaffMemberId: staffA.Value!.Id));
        Assert.True(created.IsSuccess);

        var denied = await bookings.AcceptAsync(
            tenantId,
            created.Value!.Id,
            requireStaffMemberId: staffB.Value!.Id);
        Assert.True(denied.IsFailure);
        Assert.Equal(ErrorCodes.PermissionDenied, denied.Error.Code);

        var allowed = await bookings.AcceptAsync(
            tenantId,
            created.Value.Id,
            requireStaffMemberId: staffA.Value.Id);
        Assert.True(allowed.IsSuccess);
        Assert.Equal(BookingStatus.Confirmed, allowed.Value!.Status);
    }

    private static DateTimeOffset NextMondayAt(TimeOnly time)
    {
        var today = DateTimeOffset.UtcNow;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0)
        {
            daysUntilMonday = 7;
        }

        var targetDate = today.Date.AddDays(daysUntilMonday);
        return new DateTimeOffset(targetDate + time.ToTimeSpan(), TimeSpan.Zero);
    }

    private static async Task<Guid> SeedVerifiedTenantAsync(IServiceProvider provider)
    {
        var db = provider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Scope Shop",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
            VerifiedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "hair-grooming",
            Phone = "+2348012345678",
            Description = "Scope tests",
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = $"scope-{tenantId:N}"[..24],
            Name = "Lekki",
            MarketId = "lagos",
            AddressLine = "1 Test St",
            Area = "Lekki",
            IsPrimary = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
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
        services.AddSingleton<IDistributedLockProvider, NoOpLockProvider>();
        services.AddSingleton<IFileStorage, NoopFileStorage>();
        services.Configure<MarketOptions>(options => options.DefaultTimeZoneId = "UTC");
        services.AddAdeniDomainEvents();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddCategoryWorkflowCatalog();
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<ITenantSchedulingTimeZone, TenantSchedulingTimeZone>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IEntitlementsService, EntitlementsService>();
        services.AddScoped<IBookingService, BookingService>();
        return services.BuildServiceProvider();
    }

    private sealed class NoopFileStorage : IFileStorage
    {
        public Task<string> GetUploadUrlAsync(
            string storageKey,
            string contentType,
            TimeSpan ttl,
            CancellationToken cancellationToken = default) =>
            Task.FromResult($"https://example.test/upload/{storageKey}");

        public Task<string> GetDownloadUrlAsync(
            string storageKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult($"https://example.test/{storageKey}");

        public Task SaveAsync(
            string storageKey,
            Stream content,
            string contentType,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
