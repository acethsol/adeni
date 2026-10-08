namespace Adeni.Infrastructure.Tests.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Caching;
using Adeni.Application.Storage;
using Adeni.Domain.Booking;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Tests.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class StaffServiceTests
{
    [Fact]
    public async Task CreateReplaceDeactivate_RoundTrips()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);
        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var staff = scope.ServiceProvider.GetRequiredService<IStaffService>();

        var service = await catalog.CreateAsync(
            tenantId,
            new CreateServiceOfferingRequest("Cut", null, 5000m, "NGN", 30));
        Assert.True(service.IsSuccess);

        var created = await staff.CreateAsync(
            tenantId,
            new CreateStaffMemberRequest("Ada", "Stylist", null, 0, [service.Value!.Id]));
        Assert.True(created.IsSuccess);
        Assert.Equal("Ada", created.Value!.DisplayName);
        Assert.Equal([service.Value.Id], created.Value.ServiceOfferingIds);

        var replaced = await staff.ReplaceServicesAsync(tenantId, created.Value.Id, []);
        Assert.True(replaced.IsSuccess);
        Assert.Empty(replaced.Value!.ServiceOfferingIds);

        var deactivated = await staff.DeactivateAsync(tenantId, created.Value.Id);
        Assert.True(deactivated.IsSuccess);

        var list = await staff.ListForTenantAsync(tenantId);
        Assert.False(list.Single().IsActive);
    }

    [Fact]
    public async Task ReplaceServices_RejectsOtherTenantService()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantA = await SeedVerifiedTenantAsync(scope.ServiceProvider);
        var tenantB = await SeedVerifiedTenantAsync(scope.ServiceProvider);
        var catalog = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var staff = scope.ServiceProvider.GetRequiredService<IStaffService>();

        var foreign = await catalog.CreateAsync(
            tenantB,
            new CreateServiceOfferingRequest("Foreign", null, 1000m, "NGN", 30));
        Assert.True(foreign.IsSuccess);

        var created = await staff.CreateAsync(tenantA, new CreateStaffMemberRequest("Bob"));
        Assert.True(created.IsSuccess);

        var result = await staff.ReplaceServicesAsync(
            tenantA,
            created.Value!.Id,
            [foreign.Value!.Id]);
        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    [Fact]
    public void IsStaffSlotAvailable_RespectsPerStaffCapacity()
    {
        var staffA = Guid.NewGuid();
        var staffB = Guid.NewGuid();
        var start = DateTimeOffset.Parse("2026-10-10T14:00:00Z");
        var end = start.AddMinutes(30);

        var existing = new List<BookingRecord>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                ServiceOfferingId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                StaffMemberId = staffA,
                StartAt = start,
                EndAt = end,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
        };

        Assert.False(
            BookingConflictChecker.IsStaffSlotAvailable(
                existing,
                [staffA, staffB],
                start,
                end,
                staffA));
        Assert.True(
            BookingConflictChecker.IsStaffSlotAvailable(
                existing,
                [staffA, staffB],
                start,
                end,
                staffB));
        Assert.True(
            BookingConflictChecker.IsStaffSlotAvailable(
                existing,
                [staffA, staffB],
                start,
                end,
                null));
    }

    [Fact]
    public void IsStaffSlotAvailable_StaffNotEligible_Fails()
    {
        var staffA = Guid.NewGuid();
        var start = DateTimeOffset.Parse("2026-10-10T14:00:00Z");
        var end = start.AddMinutes(30);

        Assert.False(
            BookingConflictChecker.IsStaffSlotAvailable(
                [],
                [staffA],
                start,
                end,
                Guid.NewGuid()));
    }

    private static async Task<Guid> SeedVerifiedTenantAsync(IServiceProvider provider)
    {
        var db = provider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Staff Test Shop",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
            VerifiedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "hair-grooming",
            Phone = "+2348012345678",
            Description = "Staff tests",
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
        services.AddSingleton<IFileStorage, NoopFileStorage>();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddCategoryWorkflowCatalog();
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IStaffService, StaffService>();
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
