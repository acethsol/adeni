namespace Adeni.Api.Tests.Auth;

using Adeni.Api.Auth;
using Adeni.Application.Auth;
using Adeni.Domain.Identity;

public sealed class PortalAccessHttpContextTests
{
    [Fact]
    public void ResolveBookingsStaffScope_full_bookings_is_unscoped()
    {
        var access = Snapshot(
            PortalPermissionRoles.Owner,
            PortalPermissionRoles.PermissionsFor(PortalPermissionRoles.Owner),
            staffMemberId: null);

        var (failClosed, staffId) = PortalAccessHttpContext.ResolveBookingsStaffScope(access);

        Assert.False(failClosed);
        Assert.Null(staffId);
    }

    [Fact]
    public void ResolveBookingsStaffScope_practitioner_with_link_scopes()
    {
        var staffId = Guid.NewGuid();
        var access = Snapshot(
            PortalPermissionRoles.Practitioner,
            PortalPermissionRoles.PermissionsFor(PortalPermissionRoles.Practitioner),
            staffId);

        var (failClosed, scoped) = PortalAccessHttpContext.ResolveBookingsStaffScope(access);

        Assert.False(failClosed);
        Assert.Equal(staffId, scoped);
    }

    [Fact]
    public void ResolveBookingsStaffScope_practitioner_without_link_fails_closed()
    {
        var access = Snapshot(
            PortalPermissionRoles.Practitioner,
            PortalPermissionRoles.PermissionsFor(PortalPermissionRoles.Practitioner),
            staffMemberId: null);

        var (failClosed, scoped) = PortalAccessHttpContext.ResolveBookingsStaffScope(access);

        Assert.True(failClosed);
        Assert.Null(scoped);
    }

    [Fact]
    public void CanAccessStaffMember_self_only_own_id()
    {
        var own = Guid.NewGuid();
        var other = Guid.NewGuid();
        var access = Snapshot(
            PortalPermissionRoles.Practitioner,
            PortalPermissionRoles.PermissionsFor(PortalPermissionRoles.Practitioner),
            own);

        Assert.True(PortalAccessHttpContext.CanAccessStaffMember(access, own));
        Assert.False(PortalAccessHttpContext.CanAccessStaffMember(access, other));
    }

    [Fact]
    public void CanAccessStaffMember_manager_any_id()
    {
        var access = Snapshot(
            PortalPermissionRoles.Manager,
            PortalPermissionRoles.PermissionsFor(PortalPermissionRoles.Manager),
            staffMemberId: null);

        Assert.True(PortalAccessHttpContext.CanAccessStaffMember(access, Guid.NewGuid()));
    }

    private static PortalAccessSnapshot Snapshot(
        string role,
        IReadOnlyList<string> permissions,
        Guid? staffMemberId) =>
        new(Guid.NewGuid(), Guid.NewGuid(), role, permissions, staffMemberId);
}
