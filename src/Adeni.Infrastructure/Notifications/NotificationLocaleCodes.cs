namespace Adeni.Infrastructure.Notifications;

internal static class NotificationLocaleCodes
{
    private static readonly HashSet<string> CatalogLocales = LoadCatalogLocales();

    internal static string Normalize(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return "en";
        }

        var trimmed = locale.Trim().ToLowerInvariant();
        return CatalogLocales.Contains(trimmed) ? trimmed : "en";
    }

    internal static bool IsSupported(string? locale) =>
        !string.IsNullOrWhiteSpace(locale) &&
        CatalogLocales.Contains(locale.Trim().ToLowerInvariant());

    internal static string GetDisplayName(string locale, bool native = true) =>
        locale.Trim().ToLowerInvariant() switch
        {
            "en" => native ? "English" : "English",
            "fr" => native ? "Français" : "French",
            _ => locale,
        };

    internal static string FormatBilingualSectionHeading(string locale) =>
        $"--- {GetDisplayName(locale)} ---";

    private static HashSet<string> LoadCatalogLocales()
    {
        try
        {
            var catalog = NotificationTemplateCatalogJson.LoadFromDefaultPaths();
            var locales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var template in catalog.Values)
            {
                foreach (var key in template.Keys)
                {
                    locales.Add(key.Trim().ToLowerInvariant());
                }
            }

            if (locales.Count > 0)
            {
                return locales;
            }
        }
        catch
        {
            // Fall back when catalog is unavailable (e.g. some unit tests).
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "en", "fr" };
    }
}
