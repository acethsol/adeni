namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;

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

    internal static string NormalizeLocale(string? locale) => NotificationLocaleCodes.Normalize(locale);

    internal static string NormalizeLanguageMode(string? mode) =>
        string.Equals(mode?.Trim(), "bilingual", StringComparison.OrdinalIgnoreCase) ? "bilingual" : "single";

    internal static RenderedNotificationTemplate MergeBilingualEmail(
        RenderedNotificationTemplate primary,
        RenderedNotificationTemplate secondary,
        string primaryLocale,
        string secondaryLocale)
    {
        var subject = primary.Subject == secondary.Subject
            ? primary.Subject
            : $"{primary.Subject} / {secondary.Subject}";

        var body = string.Join(
            "\n\n",
            NotificationLocaleCodes.FormatBilingualSectionHeading(primaryLocale),
            primary.Body,
            NotificationLocaleCodes.FormatBilingualSectionHeading(secondaryLocale),
            secondary.Body);

        return new RenderedNotificationTemplate(subject, body);
    }
}
