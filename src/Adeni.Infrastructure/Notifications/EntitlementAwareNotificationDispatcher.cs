namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Adeni.Application.Subscriptions;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class EntitlementAwareNotificationDispatcher(
    IdempotentNotificationDispatcher inner,
    AdeniDbContext dbContext,
    IEntitlementsService entitlementsService,
    ILogger<EntitlementAwareNotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var channel = message.Channel.Trim().ToLowerInvariant();
        if (channel is "sms" or "whatsapp" && message.TenantId is { } tenantId)
        {
            var tenant = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

            if (tenant is null)
            {
                logger.LogDebug("Skipping SMS notification — tenant {TenantId} not found.", tenantId);
                return;
            }

            var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
            if (!entitlements.SmsEnabled)
            {
                logger.LogDebug(
                    "Skipping SMS notification for tenant {TenantId} — smsEnabled entitlement false.",
                    tenantId);
                return;
            }
        }

        await inner.SendAsync(message, cancellationToken);
    }
}
