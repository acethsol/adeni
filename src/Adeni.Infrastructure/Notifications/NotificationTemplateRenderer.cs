namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

public sealed class NotificationTemplateRenderer(
    IHostEnvironment environment,
    AdeniDbContext dbContext,
    IEntitlementsService entitlementsService) : INotificationTemplateRenderer
{
    private readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<string, TemplateLocaleCopy>>> _catalog =
        new(() => NotificationTemplateCatalogJson.Load(environment));

    public async Task<Result<RenderedNotificationTemplate>> RenderAsync(
        string templateId,
        Guid? tenantId,
        string locale,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        var normalizedLocale = NotificationTemplateText.NormalizeLocale(locale);
        if (!_catalog.Value.TryGetValue(templateId, out var locales)
            || !locales.TryGetValue(normalizedLocale, out var defaults))
        {
            if (!_catalog.Value.TryGetValue(templateId, out locales)
                || !locales.TryGetValue("en", out defaults))
            {
                return Result.Failure<RenderedNotificationTemplate>(
                    Error.Validation($"Unknown notification template '{templateId}'."));
            }
        }

        var enrichedData = EnrichTemplateData(normalizedLocale, data);

        var subject = defaults.Subject;
        var body = defaults.Body;

        if (tenantId is { } tid)
        {
            var tenantOverride = await dbContext.TenantNotificationTemplateOverrides
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.TenantId == tid && x.TemplateId == templateId && x.Locale == normalizedLocale,
                    cancellationToken);

            if (tenantOverride is not null)
            {
                var tenant = await dbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == tid, cancellationToken);

                if (tenant is not null)
                {
                    var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
                    if (entitlements.ConfigurableReminders)
                    {
                        if (!string.IsNullOrWhiteSpace(tenantOverride.SubjectOverride))
                        {
                            subject = tenantOverride.SubjectOverride;
                        }

                        if (!string.IsNullOrWhiteSpace(tenantOverride.BodyOverride))
                        {
                            body = tenantOverride.BodyOverride;
                        }
                    }
                }
            }
        }

        return Result.Success(new RenderedNotificationTemplate(
            NotificationTemplateText.ApplyVariables(subject, enrichedData),
            NotificationTemplateText.ApplyVariables(body, enrichedData)));
    }

    private static Dictionary<string, string> EnrichTemplateData(
        string locale,
        IReadOnlyDictionary<string, string> data)
    {
        var enriched = data.ToDictionary(StringComparer.Ordinal);

        if (enriched.TryGetValue("startAtUtc", out var startAtRaw)
            && DateTimeOffset.TryParse(startAtRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var startAt))
        {
            enriched["startAt"] = NotificationTemplateDataFormatter.FormatInstant(startAt, locale);
        }

        if (enriched.TryGetValue("reason", out var reason))
        {
            enriched["reasonSuffix"] = NotificationTemplateDataFormatter.ReasonSuffix(reason, locale);
        }

        return enriched;
    }
}
