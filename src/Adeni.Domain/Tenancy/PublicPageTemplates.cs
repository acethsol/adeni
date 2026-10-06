namespace Adeni.Domain.Tenancy;

public static class PublicPageTemplates
{
    public const string Studio = "studio";
    public const string Spa = "spa";
    public const string Barber = "barber";
    public const string Luxe = "luxe";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Studio,
        Spa,
        Barber,
        Luxe
    };

    public static bool IsValid(string? templateId) =>
        !string.IsNullOrWhiteSpace(templateId) && All.Contains(templateId.Trim());

    public static string Normalize(string? templateId) =>
        IsValid(templateId) ? templateId!.Trim().ToLowerInvariant() : Studio;
}
