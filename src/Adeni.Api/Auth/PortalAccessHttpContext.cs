namespace Adeni.Api.Auth;

using Adeni.Application.Auth;
using Adeni.Domain.Identity;

internal static class PortalAccessHttpContext
{
    private const string ItemKey = "adeni.portalAccess";

    public static void Set(HttpContext httpContext, PortalAccessSnapshot snapshot) =>
        httpContext.Items[ItemKey] = snapshot;

    public static PortalAccessSnapshot? Get(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(ItemKey, out var value) && value is PortalAccessSnapshot snapshot
            ? snapshot
            : null;

    /// <summary>
    /// True when the user may only see their own staff-assigned bookings/calendar
    /// (has <c>portal.bookings.self</c> but not full <c>portal.bookings</c>).
    /// </summary>
    public static bool IsBookingsSelfOnly(PortalAccessSnapshot? access) =>
        access is not null
        && access.Permissions.Contains(PortalPermissions.BookingsSelf)
        && !access.Permissions.Contains(PortalPermissions.Bookings);

    /// <summary>
    /// Resolves the staff filter for bookings APIs.
    /// When self-only without a linked <see cref="PortalAccessSnapshot.StaffMemberId"/>,
    /// returns <c>failClosed: true</c> (caller must deny — never treat as unscoped).
    /// </summary>
    public static (bool FailClosed, Guid? StaffMemberId) ResolveBookingsStaffScope(
        PortalAccessSnapshot? access)
    {
        if (!IsBookingsSelfOnly(access))
        {
            return (false, null);
        }

        if (access!.StaffMemberId is { } linked)
        {
            return (false, linked);
        }

        return (true, null);
    }

    /// <summary>
    /// True when the user can administer the full roster (<c>portal.staff</c>).
    /// </summary>
    public static bool CanManageStaff(PortalAccessSnapshot? access) =>
        access is not null
        && access.Permissions.Contains(PortalPermissions.Staff);

    /// <summary>
    /// True when the user may access this staff member's self-scoped data.
    /// </summary>
    public static bool CanAccessStaffMember(PortalAccessSnapshot? access, Guid staffMemberId) =>
        CanManageStaff(access)
        || (access is not null
            && access.Permissions.Contains(PortalPermissions.StaffSelf)
            && access.StaffMemberId == staffMemberId);
}
