using Adeni.Domain.Identity;

namespace Adeni.Domain.Tests.Identity;

public sealed class PortalPermissionRolesTests
{
    [Fact]
    public void PermissionsFor_Owner_IncludesPlanAndStaff()
    {
        var permissions = PortalPermissionRoles.PermissionsFor(PortalPermissionRoles.Owner);

        Assert.Contains(PortalPermissions.Plan, permissions);
        Assert.Contains(PortalPermissions.Staff, permissions);
    }

    [Fact]
    public void PermissionsFor_Manager_ExcludesPlan()
    {
        var permissions = PortalPermissionRoles.PermissionsFor(PortalPermissionRoles.Manager);

        Assert.Contains(PortalPermissions.Staff, permissions);
        Assert.DoesNotContain(PortalPermissions.Plan, permissions);
    }

    [Fact]
    public void PermissionsFor_Practitioner_IsSelfScoped()
    {
        var permissions = PortalPermissionRoles.PermissionsFor(PortalPermissionRoles.Practitioner);

        Assert.Contains(PortalPermissions.BookingsSelf, permissions);
        Assert.Contains(PortalPermissions.StaffSelf, permissions);
        Assert.DoesNotContain(PortalPermissions.Bookings, permissions);
        Assert.DoesNotContain(PortalPermissions.Staff, permissions);
    }

    [Fact]
    public void Normalize_Unknown_BecomesOwner()
    {
        Assert.Equal(PortalPermissionRoles.Owner, PortalPermissionRoles.Normalize("legacy-admin"));
        Assert.Equal(PortalPermissionRoles.Owner, PortalPermissionRoles.Normalize(null));
    }

    [Fact]
    public void HasAny_MatchesOneOfRequired()
    {
        Assert.True(PortalPermissionRoles.HasAny(
            PortalPermissionRoles.Practitioner,
            PortalPermissions.Bookings,
            PortalPermissions.BookingsSelf));

        Assert.False(PortalPermissionRoles.HasAny(
            PortalPermissionRoles.Practitioner,
            PortalPermissions.Plan));
    }

    [Theory]
    [InlineData("barber", PortalPermissionRoles.Practitioner)]
    [InlineData("receptionist", PortalPermissionRoles.Receptionist)]
    [InlineData("supervisor", PortalPermissionRoles.Manager)]
    [InlineData("accountant", PortalPermissionRoles.Accountant)]
    [InlineData("marketing", PortalPermissionRoles.Ops)]
    public void DefaultForFloorRole_MapsInvitePrefill(string floor, string expected) =>
        Assert.Equal(expected, PortalPermissionRoles.DefaultForFloorRole(floor));
}
