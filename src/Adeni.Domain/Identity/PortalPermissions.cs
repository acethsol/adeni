namespace Adeni.Domain.Identity;

/// <summary>
/// Portal RBAC keys — what a business login can open.
/// Distinct from booking floor <c>StaffMember.RoleKey</c>.
/// Keep in sync with <c>packages/shared/src/portal-permissions.ts</c>.
/// </summary>
public static class PortalPermissions
{
    public const string Overview = "portal.overview";
    public const string Bookings = "portal.bookings";
    public const string BookingsSelf = "portal.bookings.self";
    public const string Messages = "portal.messages";
    public const string Quotes = "portal.quotes";
    public const string Services = "portal.services";
    public const string Staff = "portal.staff";
    public const string StaffSelf = "portal.staff.self";
    public const string Hours = "portal.hours";
    public const string Locations = "portal.locations";
    public const string PublicPage = "portal.public_page";
    public const string Profile = "portal.profile";
    public const string Payments = "portal.payments";
    public const string Plan = "portal.plan";

    public static readonly IReadOnlyList<string> All =
    [
        Overview,
        Bookings,
        BookingsSelf,
        Messages,
        Quotes,
        Services,
        Staff,
        StaffSelf,
        Hours,
        Locations,
        PublicPage,
        Profile,
        Payments,
        Plan,
    ];
}

/// <summary>Login permission roles (RBAC). Stored on <see cref="BusinessUser.Role"/>.</summary>
public static class PortalPermissionRoles
{
    public const string Owner = "owner";
    public const string Manager = "manager";
    public const string Receptionist = "receptionist";
    public const string Practitioner = "practitioner";
    public const string Accountant = "accountant";
    public const string Ops = "ops";

    public static readonly IReadOnlyList<string> All =
    [
        Owner,
        Manager,
        Receptionist,
        Practitioner,
        Accountant,
        Ops,
    ];

    public static bool IsValid(string? role) =>
        !string.IsNullOrWhiteSpace(role)
        && All.Contains(role.Trim().ToLowerInvariant());

    /// <summary>Unknown / legacy values become owner so existing business users keep full access.</summary>
    public static string Normalize(string? role)
    {
        var key = role?.Trim().ToLowerInvariant() ?? string.Empty;
        return IsValid(key) ? key : Owner;
    }

    public static IReadOnlyList<string> PermissionsFor(string? role) =>
        Normalize(role) switch
        {
            Manager => PortalPermissions.All.Where(p => p != PortalPermissions.Plan).ToArray(),
            Receptionist =>
            [
                PortalPermissions.Overview,
                PortalPermissions.Bookings,
                PortalPermissions.Messages,
                PortalPermissions.Quotes,
                PortalPermissions.Services,
                PortalPermissions.Hours,
            ],
            Practitioner =>
            [
                PortalPermissions.Overview,
                PortalPermissions.BookingsSelf,
                PortalPermissions.StaffSelf,
                PortalPermissions.Messages,
            ],
            Accountant =>
            [
                PortalPermissions.Overview,
                PortalPermissions.Payments,
            ],
            Ops =>
            [
                PortalPermissions.Overview,
                PortalPermissions.Services,
                PortalPermissions.Hours,
                PortalPermissions.Locations,
                PortalPermissions.PublicPage,
            ],
            _ => PortalPermissions.All,
        };

    public static bool Has(string? role, string permission) =>
        PermissionsFor(role).Contains(permission);

    public static bool HasAny(string? role, params string[] permissions) =>
        permissions.Any(p => Has(role, p));
}
