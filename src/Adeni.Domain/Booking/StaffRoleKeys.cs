namespace Adeni.Domain.Booking;

/// <summary>
/// Roster role keys. Practitioner keys are category-scoped in the portal UI;
/// ops keys align with Zenoti-style center roles (accountant, manager, …).
/// </summary>
public static class StaffRoleKeys
{
    public const string Stylist = "stylist";
    public const string Barber = "barber";
    public const string NailTech = "nail_tech";
    public const string Esthetician = "esthetician";
    public const string Therapist = "therapist";
    public const string Instructor = "instructor";
    public const string Trainer = "trainer";
    public const string Receptionist = "receptionist";
    public const string Supervisor = "supervisor";
    public const string Manager = "manager";
    public const string Accountant = "accountant";
    public const string InventoryManager = "inventory_manager";
    public const string Marketing = "marketing";
    public const string Hr = "hr";
    public const string AdminStaff = "admin_staff";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All =
    [
        Stylist,
        Barber,
        NailTech,
        Esthetician,
        Therapist,
        Instructor,
        Trainer,
        Receptionist,
        Supervisor,
        Manager,
        Accountant,
        InventoryManager,
        Marketing,
        Hr,
        AdminStaff,
        Other,
    ];

    public static bool IsValid(string? roleKey) =>
        !string.IsNullOrWhiteSpace(roleKey)
        && All.Contains(roleKey.Trim().ToLowerInvariant());

    public static string Normalize(string? roleKey) =>
        IsValid(roleKey) ? roleKey!.Trim().ToLowerInvariant() : Other;
}
