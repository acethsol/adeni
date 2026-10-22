namespace Adeni.Infrastructure.Notifications;

using System.Globalization;

internal static class NotificationTemplateDataFormatter
{
    internal static string FormatInstant(DateTimeOffset instant, string locale)
    {
        var culture = locale.StartsWith("fr", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo("fr-CA")
            : CultureInfo.GetCultureInfo("en-CA");

        return instant.ToString("f", culture);
    }

    internal static string ReasonSuffix(string? reason, string locale)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return string.Empty;
        }

        var label = locale.StartsWith("fr", StringComparison.OrdinalIgnoreCase) ? " Raison :" : " Reason:";
        return $"{label} {reason.Trim()}";
    }
}
