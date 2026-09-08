namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Adeni.Domain.Common;
using Adeni.Domain.Notifications;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class NotificationPreferencesService(AdeniDbContext dbContext) : INotificationPreferencesService
{
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
        prefs.SmsWhatsAppReminderEnabled = request.SmsWhatsAppReminderEnabled;
        prefs.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(Map(prefs));
    }

    public async Task<NotificationPreferencesResponse> GetOrDefaultsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var prefs = await dbContext.TenantNotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        return prefs is null ? DefaultPreferences() : Map(prefs);
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

    private static NotificationPreferencesResponse DefaultPreferences() =>
        new(EmailEnabled: true, PushEnabled: false, SmsWhatsAppReminderEnabled: false);

    private static NotificationPreferencesResponse Map(TenantNotificationPreferences prefs) =>
        new(prefs.EmailEnabled, prefs.PushEnabled, prefs.SmsWhatsAppReminderEnabled);
}
