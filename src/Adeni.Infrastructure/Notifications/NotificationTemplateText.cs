namespace Adeni.Infrastructure.Notifications;

internal static class NotificationTemplateText
{
    internal static string ApplyVariables(string text, IReadOnlyDictionary<string, string> data)
    {
        var result = text;
        foreach (var (key, value) in data)
        {
            result = result.Replace($"{{{key}}}", value, StringComparison.Ordinal);
        }

        return result;
    }

    internal static string NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return "en";
        }

        var trimmed = locale.Trim().ToLowerInvariant();
        return trimmed.StartsWith("fr", StringComparison.Ordinal) ? "fr" : "en";
    }
}
