using Adeni.Domain.Identity;

namespace Adeni.Domain.Tests.Identity;

public sealed class BusinessPermissionRolesTests
{
    [Fact]
    public void PermissionsFor_Owner_IncludesPlanAndStaff()
    {
        var permissions = BusinessPermissionRoles.PermissionsFor(BusinessPermissionRoles.Owner);

        Assert.Contains(BusinessPermissions.Plan, permissions);
        Assert.Contains(BusinessPermissions.Staff, permissions);
    }

    [Fact]
    public void PermissionsFor_Manager_ExcludesPlan()
    {
        var permissions = BusinessPermissionRoles.PermissionsFor(BusinessPermissionRoles.Manager);

        Assert.Contains(BusinessPermissions.Staff, permissions);
        Assert.DoesNotContain(BusinessPermissions.Plan, permissions);
    }

    [Fact]
    public void PermissionsFor_Practitioner_IsSelfScoped()
    {
        var permissions = BusinessPermissionRoles.PermissionsFor(BusinessPermissionRoles.Practitioner);

        Assert.Contains(BusinessPermissions.BookingsSelf, permissions);
        Assert.Contains(BusinessPermissions.StaffSelf, permissions);
        Assert.DoesNotContain(BusinessPermissions.Bookings, permissions);
        Assert.DoesNotContain(BusinessPermissions.Staff, permissions);
    }

    [Fact]
    public void Normalize_Unknown_BecomesOwner()
    {
        Assert.Equal(BusinessPermissionRoles.Owner, BusinessPermissionRoles.Normalize("legacy-admin"));
        Assert.Equal(BusinessPermissionRoles.Owner, BusinessPermissionRoles.Normalize(null));
    }

    [Fact]
    public void HasAny_MatchesOneOfRequired()
    {
        Assert.True(BusinessPermissionRoles.HasAny(
            BusinessPermissionRoles.Practitioner,
            BusinessPermissions.Bookings,
            BusinessPermissions.BookingsSelf));

        Assert.False(BusinessPermissionRoles.HasAny(
            BusinessPermissionRoles.Practitioner,
            BusinessPermissions.Plan));
    }

    [Theory]
    [InlineData("barber", BusinessPermissionRoles.Practitioner)]
    [InlineData("receptionist", BusinessPermissionRoles.Receptionist)]
    [InlineData("supervisor", BusinessPermissionRoles.Manager)]
    [InlineData("accountant", BusinessPermissionRoles.Accountant)]
    [InlineData("marketing", BusinessPermissionRoles.Ops)]
    public void DefaultForFloorRole_MapsInvitePrefill(string floor, string expected) =>
        Assert.Equal(expected, BusinessPermissionRoles.DefaultForFloorRole(floor));
}
