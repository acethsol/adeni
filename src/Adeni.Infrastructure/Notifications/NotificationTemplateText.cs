namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;

internal static class NotificationTemplateText
{
    internal const string BilingualEmailEnglishHeading = "--- English ---";
    internal const string BilingualEmailFrenchHeading = "--- Français ---";
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

    internal static string NormalizeLanguageMode(string? mode) =>
        string.Equals(mode?.Trim(), "bilingual", StringComparison.OrdinalIgnoreCase) ? "bilingual" : "single";

    internal static RenderedNotificationTemplate MergeBilingualEmail(
        RenderedNotificationTemplate english,
        RenderedNotificationTemplate french)
    {
        var subject = english.Subject == french.Subject
            ? english.Subject
            : $"{english.Subject} / {french.Subject}";

        var body = string.Join(
            "\n\n",
            BilingualEmailEnglishHeading,
            english.Body,
            BilingualEmailFrenchHeading,
            french.Body);

        return new RenderedNotificationTemplate(subject, body);
    }
}
