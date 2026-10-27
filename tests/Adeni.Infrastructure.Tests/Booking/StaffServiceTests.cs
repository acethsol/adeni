namespace Adeni.Infrastructure.Tests.Booking;

using Adeni.Infrastructure.Tests.Subscriptions;
using Adeni.Application.Booking;
using Adeni.Application.Caching;
using Adeni.Application.Storage;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Subscriptions;
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
            new CreateStaffMemberRequest(
                "Ada",
                "Okafor",
                DisplayName: "Ada",
                RoleKey: StaffRoleKeys.Stylist,
                Title: "Senior stylist",
                ServiceOfferingIds: [service.Value!.Id]));
        Assert.True(created.IsSuccess);
        Assert.Equal("Ada", created.Value!.FirstName);
        Assert.Equal("Okafor", created.Value.LastName);
        Assert.Equal("Ada", created.Value.DisplayName);
        Assert.Equal(StaffRoleKeys.Stylist, created.Value.RoleKey);
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

        var created = await staff.CreateAsync(
            tenantA,
            new CreateStaffMemberRequest("Bob", "Mensah"));
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

    [Fact]
    public async Task HoursAndLeave_RoundTrip_AndConflictWarning()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);
        var staff = scope.ServiceProvider.GetRequiredService<IStaffService>();

        var created = await staff.CreateAsync(tenantId, new CreateStaffMemberRequest("Chi", "Ade"));
        Assert.True(created.IsSuccess);
        var staffId = created.Value!.Id;

        var hours = await staff.ReplaceHoursAsync(
            tenantId,
            staffId,
            [new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(14, 0))]);
        Assert.True(hours.IsSuccess);
        Assert.Single(hours.Value!);

        var leaveStart = DateTimeOffset.UtcNow.AddDays(2);
        var leave = await staff.CreateLeaveAsync(
            tenantId,
            staffId,
            new CreateStaffLeaveRequest(leaveStart, leaveStart.AddHours(4), "Personal"));
        Assert.True(leave.IsSuccess);
        Assert.Empty(leave.Value!.ConflictingBookingIds);

        var listed = await staff.ListLeaveAsync(tenantId, staffId);
        Assert.True(listed.IsSuccess);
        Assert.Single(listed.Value!);

        var deleted = await staff.DeleteLeaveAsync(tenantId, staffId, leave.Value.Id);
        Assert.True(deleted.IsSuccess);
    }

    [Fact]
    public void EffectiveRules_IntersectBusinessAndStaff()
    {
        var business = new[]
        {
            new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
        };
        var staff = new[]
        {
            new WeeklyAvailabilityRule(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(14, 0)),
        };

        var effective = StaffScheduleHelper.EffectiveRules(business, staff);
        Assert.Single(effective);
        Assert.Equal(new TimeOnly(10, 0), effective[0].OpenTime);
        Assert.Equal(new TimeOnly(14, 0), effective[0].CloseTime);

        var inherit = StaffScheduleHelper.EffectiveRules(business, []);
        Assert.Equal(business, inherit);
    }

    [Fact]
    public async Task ListForTenant_FiltersByBusinessLocationAssignment()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);
        var staff = scope.ServiceProvider.GetRequiredService<IStaffService>();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var locA = Guid.NewGuid();
        var locB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.BusinessLocations.AddRange(
            new BusinessLocation
            {
                Id = locA,
                TenantId = tenantId,
                Name = "A",
                Slug = "loc-a-staff",
                MarketId = "lagos",
                AddressLine = "1",
                Area = "A",
                IsPrimary = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new BusinessLocation
            {
                Id = locB,
                TenantId = tenantId,
                Name = "B",
                Slug = "loc-b-staff",
                MarketId = "lagos",
                AddressLine = "2",
                Area = "B",
                IsPrimary = false,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
        await db.SaveChangesAsync();

        var allBranches = await staff.CreateAsync(tenantId, new CreateStaffMemberRequest("All", "Branches"));
        var branchOnly = await staff.CreateAsync(
            tenantId,
            new CreateStaffMemberRequest("Branch", "A", BusinessLocationId: locA));
        Assert.True(allBranches.IsSuccess && branchOnly.IsSuccess);

        await staff.UpdateAsync(
            tenantId,
            branchOnly.Value!.Id,
            new UpdateStaffMemberRequest(
                "Branch",
                "A",
                "Branch A",
                StaffRoleKeys.Other,
                null,
                null,
                0,
                true,
                locA));

        var scoped = await staff.ListForTenantAsync(tenantId, locA);
        Assert.Single(scoped);
        Assert.Equal(branchOnly.Value!.Id, scoped[0].Id);

        var otherBranch = await staff.ListForTenantAsync(tenantId, locB);
        Assert.Empty(otherBranch);

        var all = await staff.ListForTenantAsync(tenantId);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetTeamCalendar_FiltersStaffByBusinessLocationAssignment()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);
        var staff = scope.ServiceProvider.GetRequiredService<IStaffService>();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var locA = Guid.NewGuid();
        var locB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.BusinessLocations.AddRange(
            new BusinessLocation
            {
                Id = locA,
                TenantId = tenantId,
                Name = "A",
                Slug = "loc-a-team-cal",
                MarketId = "lagos",
                AddressLine = "1",
                Area = "A",
                IsPrimary = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new BusinessLocation
            {
                Id = locB,
                TenantId = tenantId,
                Name = "B",
                Slug = "loc-b-team-cal",
                MarketId = "lagos",
                AddressLine = "2",
                Area = "B",
                IsPrimary = false,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
        await db.SaveChangesAsync();

        var branchA = await staff.CreateAsync(
            tenantId,
            new CreateStaffMemberRequest("Branch", "A", BusinessLocationId: locA));
        var branchB = await staff.CreateAsync(
            tenantId,
            new CreateStaffMemberRequest("Branch", "B", BusinessLocationId: locB));
        Assert.True(branchA.IsSuccess && branchB.IsSuccess);

        var from = now;
        var to = now.AddDays(7);

        var scoped = await staff.GetTeamCalendarAsync(tenantId, from, to, locA);
        Assert.True(scoped.IsSuccess);
        Assert.Single(scoped.Value!.Staff);
        Assert.Equal(branchA.Value!.Id, scoped.Value.Staff[0].StaffMemberId);

        var allBranches = await staff.GetTeamCalendarAsync(tenantId, from, to);
        Assert.True(allBranches.IsSuccess);
        Assert.Equal(2, allBranches.Value!.Staff.Count);
    }

    [Fact]
    public async Task ListForTenant_IncludesPortalAccessStatus()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var tenantId = await SeedVerifiedTenantAsync(scope.ServiceProvider);
        var staff = scope.ServiceProvider.GetRequiredService<IStaffService>();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();

        var none = await staff.CreateAsync(tenantId, new CreateStaffMemberRequest("No", "Access"));
        var pending = await staff.CreateAsync(tenantId, new CreateStaffMemberRequest("Invite", "Pending"));
        var active = await staff.CreateAsync(tenantId, new CreateStaffMemberRequest("Has", "Login"));
        Assert.True(none.IsSuccess && pending.IsSuccess && active.IsSuccess);

        db.StaffPortalInvites.Add(new StaffPortalInvite
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "pending@example.com",
            PermissionRole = PortalPermissionRoles.Practitioner,
            StaffMemberId = pending.Value!.Id,
            TokenHash = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant(),
            Status = StaffPortalInviteStatuses.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(3),
            CreatedAt = DateTimeOffset.UtcNow,
            InvitedByAuth0Sub = "auth0|owner",
        });
        db.BusinessUsers.Add(new BusinessUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Auth0Sub = "auth0|active-staff",
            Role = PortalPermissionRoles.Practitioner,
            StaffMemberId = active.Value!.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var list = await staff.ListForTenantAsync(tenantId);
        Assert.Equal(PortalAccessStatuses.None, list.Single(x => x.Id == none.Value!.Id).PortalAccessStatus);
        Assert.Equal(PortalAccessStatuses.InvitePending, list.Single(x => x.Id == pending.Value!.Id).PortalAccessStatus);
        Assert.NotNull(list.Single(x => x.Id == pending.Value!.Id).PortalInviteId);
        var activeRow = list.Single(x => x.Id == active.Value!.Id);
        Assert.Equal(PortalAccessStatuses.Active, activeRow.PortalAccessStatus);
        Assert.Equal(PortalPermissionRoles.Practitioner, activeRow.PermissionRole);
        Assert.NotNull(activeRow.BusinessUserId);
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
        services.AddEntitlementsModule();
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

        public Task<byte[]?> ReadBytesAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]?>(null);

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
