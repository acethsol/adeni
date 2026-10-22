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
    INotificationJobScheduler notificationJobs,
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

        var subject = "A slot opened up";
        var body =
            $"Good news — a slot is available at {domainEvent.StartAt:u}. Book soon before it is taken.";

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
                await notificationJobs.ScheduleSendAsync(
                    new NotificationMessage(
                        "email",
                        contact.Value.Email.Trim(),
                        subject,
                        body,
                        domainEvent.TenantId,
                        dedupeBase + ":email"),
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(contact.Value.PhoneE164))
            {
                await notificationJobs.ScheduleSendAsync(
                    new NotificationMessage(
                        "sms",
                        contact.Value.PhoneE164.Trim(),
                        subject,
                        body,
                        domainEvent.TenantId,
                        dedupeBase + ":sms"),
                    cancellationToken);
            }

            entry.NotifiedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
