namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Events;
using Adeni.Application.Notifications;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Booking.Events;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class WaitlistNotificationHandler(
    AdeniDbContext dbContext,
    INotificationDispatcher notificationDispatcher,
    INotificationRecipientResolver recipientResolver,
    IEntitlementsService entitlementsService,
    IOptions<NotificationsOptions> options,
    ILogger<WaitlistNotificationHandler> logger) : IDomainEventHandler<SlotOpened>
{
    public async Task HandleAsync(SlotOpened domainEvent, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == domainEvent.TenantId, cancellationToken);

        if (tenant is null)
        {
            return;
        }

        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
        if (!entitlements.Waitlist)
        {
            logger.LogDebug(
                "Skipping waitlist slot notifications for tenant {TenantId} — waitlist not entitled.",
                domainEvent.TenantId);
            return;
        }

        var maxRecipients = Math.Max(1, options.Value.MaxWaitlistNotificationsPerSlot);
        var slotStart = domainEvent.StartAt;

        var entries = await dbContext.WaitlistEntries
            .Where(x =>
                x.TenantId == domainEvent.TenantId
                && x.ServiceOfferingId == domainEvent.ServiceOfferingId
                && x.NotifiedAt == null
                && (x.PreferredFrom == null || x.PreferredFrom <= slotStart)
                && (x.PreferredTo == null || x.PreferredTo >= slotStart))
            .OrderBy(x => x.CreatedAt)
            .Take(maxRecipients)
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
        {
            logger.LogDebug(
                "No waitlist entries to notify for tenant {TenantId} service {ServiceId}.",
                domainEvent.TenantId,
                domainEvent.ServiceOfferingId);
            return;
        }

        var data = new Dictionary<string, string>
        {
            ["startAtUtc"] = slotStart.ToString("o"),
        };

        foreach (var entry in entries)
        {
            var contact = await recipientResolver.ResolvePlatformCustomerAsync(entry.CustomerId, cancellationToken);
            if (contact.IsFailure)
            {
                continue;
            }

            var dedupeBase = $"waitlist-slot:{entry.Id:D}:{slotStart.UtcDateTime:O}";

            if (!string.IsNullOrWhiteSpace(contact.Value!.Email))
            {
                await notificationDispatcher.SendAsync(
                    NotificationMessage.FromTemplate(
                        "email",
                        contact.Value.Email.Trim(),
                        NotificationTemplateIds.WaitlistSlotOpened,
                        data,
                        domainEvent.TenantId,
                        dedupeBase + ":email",
                        entry.CustomerId),
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(contact.Value.PhoneE164))
            {
                await notificationDispatcher.SendAsync(
                    NotificationMessage.FromTemplate(
                        "sms",
                        contact.Value.PhoneE164.Trim(),
                        NotificationTemplateIds.WaitlistSlotOpened,
                        data,
                        domainEvent.TenantId,
                        dedupeBase + ":sms",
                        entry.CustomerId),
                    cancellationToken);
            }

            entry.NotifiedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
