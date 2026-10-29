namespace Adeni.Domain.Identity;

/// <summary>
/// Business app RBAC keys — what a business login can open.
/// Distinct from booking floor <c>StaffMember.RoleKey</c>.
/// Keep in sync with <c>packages/shared/src/business-permissions.ts</c>.
/// </summary>
public static class BusinessPermissions
{
    public const string Overview = "business.overview";
    public const string Bookings = "business.bookings";
    public const string BookingsSelf = "business.bookings.self";
    public const string Messages = "business.messages";
    public const string Quotes = "business.quotes";
    public const string Services = "business.services";
    public const string Staff = "business.staff";
    public const string StaffSelf = "business.staff.self";
    public const string Hours = "business.hours";
    public const string Locations = "business.locations";
    public const string PublicPage = "business.public_page";
    public const string Profile = "business.profile";
    public const string Finance = "business.finance";
    public const string Plan = "business.plan";
    public const string Retention = "business.retention";

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
        Finance,
        Plan,
        Retention,
    ];
}

/// <summary>Login permission roles (RBAC). Stored on <see cref="BusinessUser.Role"/>.</summary>
public static class BusinessPermissionRoles
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
            Manager => BusinessPermissions.All.Where(p => p != BusinessPermissions.Plan).ToArray(),
            Receptionist =>
            [
                BusinessPermissions.Overview,
                BusinessPermissions.Bookings,
                BusinessPermissions.Messages,
                BusinessPermissions.Quotes,
                BusinessPermissions.Services,
                BusinessPermissions.Hours,
                BusinessPermissions.Retention,
            ],
            Practitioner =>
            [
                BusinessPermissions.Overview,
                BusinessPermissions.BookingsSelf,
                BusinessPermissions.StaffSelf,
                BusinessPermissions.Messages,
            ],
            Accountant =>
            [
                BusinessPermissions.Overview,
                BusinessPermissions.Finance,
            ],
            Ops =>
            [
                BusinessPermissions.Overview,
                BusinessPermissions.Services,
                BusinessPermissions.Hours,
                BusinessPermissions.Locations,
                BusinessPermissions.PublicPage,
            ],
            _ => BusinessPermissions.All,
        };

    public static bool Has(string? role, string permission) =>
        PermissionsFor(role).Contains(permission);

    public static bool HasAny(string? role, params string[] permissions) =>
        permissions.Any(p => Has(role, p));

    /// <summary>Floor <c>StaffMember.RoleKey</c> → default Business app RBAC role when inviting.</summary>
    public static string DefaultForFloorRole(string? roleKey) =>
        (roleKey ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "receptionist" => Receptionist,
            "manager" or "supervisor" => Manager,
            "accountant" => Accountant,
            "inventory_manager" or "marketing" or "hr" or "admin_staff" or "other" => Ops,
            _ => Practitioner,
        };
}
