namespace Adeni.Domain.Booking;

public static class StaffRoleKeys
{
    public const string Stylist = "stylist";
    public const string Barber = "barber";
    public const string NailTech = "nail_tech";
    public const string Esthetician = "esthetician";
    public const string Therapist = "therapist";
    public const string Receptionist = "receptionist";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All =
    [
        Stylist,
        Barber,
        NailTech,
        Esthetician,
        Therapist,
        Receptionist,
        Other,
    ];

    public static bool IsValid(string? roleKey) =>
        !string.IsNullOrWhiteSpace(roleKey)
        && All.Contains(roleKey.Trim().ToLowerInvariant());

    public static string Normalize(string? roleKey) =>
        IsValid(roleKey) ? roleKey!.Trim().ToLowerInvariant() : Other;
}
