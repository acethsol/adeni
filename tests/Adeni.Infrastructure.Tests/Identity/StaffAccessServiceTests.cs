namespace Adeni.Infrastructure.Tests.Identity;

using Adeni.Application.Auth;
using Adeni.Application.Notifications;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class StaffAccessServiceTests
{
    [Fact]
    public async Task Invite_and_accept_attaches_business_user_without_new_tenant()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Lekki Cuts",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.StaffMembers.Add(new StaffMember
        {
            Id = staffId,
            TenantId = tenantId,
            FirstName = "Baba",
            LastName = "Fela",
            DisplayName = "Baba Fela",
            RoleKey = "barber",
            IsActive = true,
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var access = scope.ServiceProvider.GetRequiredService<StaffAccessService>();
        var invite = await access.InviteStaffMemberAsync(
            tenantId,
            staffId,
            new CreateStaffInviteRequest("fela@lekki.cuts", null),
            "auth0|owner",
            CancellationToken.None);

        Assert.True(invite.IsSuccess);
        Assert.Equal(PortalPermissionRoles.Practitioner, invite.Value!.PermissionRole);

        // Token is emailed once; for the unit test we control the stored hash.
        var plain = "unit-test-invite-token-value";
        var stored = await db.StaffPortalInvites.SingleAsync();
        stored.TokenHash = StaffAccessService.HashToken(plain);
        await db.SaveChangesAsync();

        var accept = await access.AcceptAsync(
            "auth0|staff-fela",
            "fela@lekki.cuts",
            new AcceptStaffInviteRequest(plain),
            CancellationToken.None);

        Assert.True(accept.IsSuccess);
        Assert.Equal(tenantId, accept.Value!.TenantId);
        Assert.Equal(staffId, accept.Value.StaffMemberId);
        Assert.Equal(1, await db.Tenants.CountAsync());

        var user = await db.BusinessUsers.IgnoreQueryFilters().SingleAsync(u => u.Auth0Sub == "auth0|staff-fela");
        Assert.Equal(PortalPermissionRoles.Practitioner, user.Role);
        Assert.Equal(staffId, user.StaffMemberId);
    }

    [Fact]
    public async Task Accept_unknown_token_fails_closed()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<StaffAccessService>();

        var result = await access.AcceptAsync(
            "auth0|nobody",
            null,
            new AcceptStaffInviteRequest("not-a-real-token"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.StaffInviteInvalid, result.Error.Code);
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        Assert.Empty(await db.BusinessUsers.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.Tenants.ToListAsync());
    }

    [Theory]
    [InlineData("receptionist", PortalPermissionRoles.Receptionist)]
    [InlineData("manager", PortalPermissionRoles.Manager)]
    [InlineData("barber", PortalPermissionRoles.Practitioner)]
    public void DefaultForFloorRole_maps_expected(string floor, string expected) =>
        Assert.Equal(expected, PortalPermissionRoles.DefaultForFloorRole(floor));

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Portal:PublicBaseUrl"] = "http://localhost:5173",
            }).Build());
        services.AddLogging();
        services.AddSingleton<INotificationDispatcher, NoopNotifications>();
        services.AddScoped<StaffAccessService>();
        return services.BuildServiceProvider();
    }

    private sealed class NoopNotifications : INotificationDispatcher
    {
        public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
