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
            var english = await templateRenderer.RenderAsync(
                templateId,
                tenantId,
                "en",
                data,
                cancellationToken);
            if (english.IsFailure)
            {
                return english;
            }

            var french = await templateRenderer.RenderAsync(
                templateId,
                tenantId,
                "fr",
                data,
                cancellationToken);
            if (french.IsFailure)
            {
                return french;
            }

            return Result.Success(
                NotificationTemplateText.MergeBilingualEmail(english.Value!, french.Value!));
        }

        return await templateRenderer.RenderAsync(
            templateId,
            tenantId,
            resolvedLocale,
            data,
            cancellationToken);
    }
}
