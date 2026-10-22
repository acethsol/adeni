namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Domain.Notifications;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class NotificationPreferencesService(
    AdeniDbContext dbContext,
    IEntitlementsService entitlementsService) : INotificationPreferencesService
{
    private const int MaxOverrides = 32;
    private const int MaxSubjectLength = 200;
    private const int MaxBodyLength = 2000;

    public async Task<Result<NotificationPreferencesResponse>> GetAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<NotificationPreferencesResponse>(access.Error);
        }

        return Result.Success(await GetOrDefaultsAsync(tenantId, cancellationToken));
    }

    public async Task<Result<NotificationPreferencesResponse>> UpdateAsync(
        Guid tenantId,
        UpdateNotificationPreferencesRequest request,
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<NotificationPreferencesResponse>(access.Error);
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<NotificationPreferencesResponse>(Error.NotFound("Business"));
        }

        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);

        if (request.SmsRemindersEnabled && !entitlements.SmsEnabled)
        {
            return Result.Failure<NotificationPreferencesResponse>(ErrorCodes.SmsNotEntitledError());
        }

        var locale = NotificationTemplateText.NormalizeLocale(request.DefaultNotificationLocale);

        var overrides = request.TemplateOverrides ?? [];
        if (overrides.Count > MaxOverrides)
        {
            return Result.Failure<NotificationPreferencesResponse>(
                Error.Validation($"At most {MaxOverrides} template overrides are allowed."));
        }

        if (overrides.Count > 0 && !entitlements.ConfigurableReminders)
        {
            return Result.Failure<NotificationPreferencesResponse>(
                Error.Validation("Custom notification templates require a Professional or Business plan."));
        }

        foreach (var item in overrides)
        {
            if (string.IsNullOrWhiteSpace(item.TemplateId))
            {
                return Result.Failure<NotificationPreferencesResponse>(
                    Error.Validation("Template id is required for each override."));
            }

            if (item.Subject is { Length: > MaxSubjectLength })
            {
                return Result.Failure<NotificationPreferencesResponse>(
                    Error.Validation($"Subject must be at most {MaxSubjectLength} characters."));
            }

            if (item.Body is { Length: > MaxBodyLength })
            {
                return Result.Failure<NotificationPreferencesResponse>(
                    Error.Validation($"Body must be at most {MaxBodyLength} characters."));
            }

            if (string.IsNullOrWhiteSpace(item.Subject) && string.IsNullOrWhiteSpace(item.Body))
            {
                return Result.Failure<NotificationPreferencesResponse>(
                    Error.Validation("Each override must include a subject and/or body."));
            }
        }

        var prefs = await dbContext.TenantNotificationPreferences
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (prefs is null)
        {
            prefs = new TenantNotificationPreferences
            {
                TenantId = tenantId,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            dbContext.TenantNotificationPreferences.Add(prefs);
        }

        prefs.EmailEnabled = request.EmailEnabled;
        prefs.PushEnabled = request.PushEnabled;
        prefs.SmsRemindersEnabled = request.SmsRemindersEnabled;
        prefs.DefaultNotificationLocale = locale;
        prefs.UpdatedAt = DateTimeOffset.UtcNow;

        var existingOverrides = await dbContext.TenantNotificationTemplateOverrides
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        dbContext.TenantNotificationTemplateOverrides.RemoveRange(existingOverrides);

        foreach (var item in overrides)
        {
            dbContext.TenantNotificationTemplateOverrides.Add(new TenantNotificationTemplateOverride
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TemplateId = item.TemplateId.Trim(),
                Locale = NotificationTemplateText.NormalizeLocale(item.Locale),
                SubjectOverride = string.IsNullOrWhiteSpace(item.Subject) ? null : item.Subject.Trim(),
                BodyOverride = string.IsNullOrWhiteSpace(item.Body) ? null : item.Body.Trim(),
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(await GetOrDefaultsAsync(tenantId, cancellationToken));
    }

    public async Task<NotificationPreferencesResponse> GetOrDefaultsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var prefs = await dbContext.TenantNotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        var overrides = await dbContext.TenantNotificationTemplateOverrides
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.TemplateId)
            .ThenBy(x => x.Locale)
            .Select(x => new NotificationTemplateOverrideDto(
                x.TemplateId,
                x.Locale,
                x.SubjectOverride,
                x.BodyOverride))
            .ToListAsync(cancellationToken);

        return prefs is null
            ? DefaultPreferences(overrides)
            : Map(prefs, overrides);
    }

    private async Task<Result> ResolveAccessAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken)
    {
        var hasAccess = await dbContext.BusinessUsers
            .AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Auth0Sub == auth0Sub, cancellationToken);

        return hasAccess
            ? Result.Success()
            : Result.Failure(Error.Forbidden("You do not have access to this business."));
    }

    private static NotificationPreferencesResponse DefaultPreferences(
        IReadOnlyList<NotificationTemplateOverrideDto> overrides) =>
        new(
            EmailEnabled: true,
            PushEnabled: false,
            SmsRemindersEnabled: false,
            DefaultNotificationLocale: "en",
            TemplateOverrides: overrides);

    private static NotificationPreferencesResponse Map(
        TenantNotificationPreferences prefs,
        IReadOnlyList<NotificationTemplateOverrideDto> overrides) =>
        new(
            prefs.EmailEnabled,
            prefs.PushEnabled,
            prefs.SmsRemindersEnabled,
            NotificationTemplateText.NormalizeLocale(prefs.DefaultNotificationLocale),
            overrides);
}
