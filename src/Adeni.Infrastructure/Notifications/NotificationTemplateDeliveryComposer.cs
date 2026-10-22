namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Adeni.Domain.Common;

public sealed class NotificationTemplateDeliveryComposer(
    INotificationTemplateRenderer templateRenderer,
    INotificationPreferencesService preferencesService)
{
    public async Task<Result<RenderedNotificationTemplate>> RenderForDeliveryAsync(
        string channel,
        string templateId,
        Guid? tenantId,
        string resolvedLocale,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        if (tenantId is not { } tid)
        {
            return await templateRenderer.RenderAsync(
                templateId,
                tenantId,
                resolvedLocale,
                data,
                cancellationToken);
        }

        var prefs = await preferencesService.GetOrDefaultsAsync(tid, cancellationToken);
        var mode = NotificationTemplateText.NormalizeLanguageMode(prefs.NotificationLanguageMode);
        var normalizedChannel = channel.Trim().ToLowerInvariant();

        if (mode == "bilingual" && normalizedChannel == "sms")
        {
            return await templateRenderer.RenderAsync(
                templateId,
                tenantId,
                prefs.DefaultNotificationLocale,
                data,
                cancellationToken);
        }

        if (mode == "bilingual" && normalizedChannel == "email")
        {
            var primaryLocale = NotificationTemplateText.NormalizeLocale(prefs.DefaultNotificationLocale);
            var secondaryLocale = ResolveSecondaryLocale(prefs, primaryLocale);

            var primary = await templateRenderer.RenderAsync(
                templateId,
                tenantId,
                primaryLocale,
                data,
                cancellationToken);
            if (primary.IsFailure)
            {
                return primary;
            }

            var secondary = await templateRenderer.RenderAsync(
                templateId,
                tenantId,
                secondaryLocale,
                data,
                cancellationToken);
            if (secondary.IsFailure)
            {
                return secondary;
            }

            return Result.Success(
                NotificationTemplateText.MergeBilingualEmail(
                    primary.Value!,
                    secondary.Value!,
                    primaryLocale,
                    secondaryLocale));
        }

        return await templateRenderer.RenderAsync(
            templateId,
            tenantId,
            resolvedLocale,
            data,
            cancellationToken);
    }

    private static string ResolveSecondaryLocale(
        NotificationPreferencesResponse prefs,
        string primaryLocale)
    {
        if (!string.IsNullOrWhiteSpace(prefs.SecondaryNotificationLocale))
        {
            var normalized = NotificationTemplateText.NormalizeLocale(prefs.SecondaryNotificationLocale);
            if (!string.Equals(normalized, primaryLocale, StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }
        }

        foreach (var candidate in new[] { "en", "fr" })
        {
            if (!string.Equals(candidate, primaryLocale, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return primaryLocale == "en" ? "fr" : "en";
    }
}
