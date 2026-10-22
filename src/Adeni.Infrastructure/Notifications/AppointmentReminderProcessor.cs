namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Booking;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class AppointmentReminderProcessor(
    AdeniDbContext dbContext,
    INotificationDispatcher notificationDispatcher,
    INotificationRecipientResolver recipientResolver,
    IEntitlementsService entitlementsService,
    IOptions<NotificationsOptions> options)
{
    private static readonly TimeSpan WindowHalfWidth = TimeSpan.FromMinutes(7);

    public async Task ProcessDueRemindersAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await ProcessWindowAsync(now, TimeSpan.FromHours(24), "24h", cancellationToken);
        await ProcessWindowAsync(now, TimeSpan.FromHours(2), "2h", cancellationToken);
        await ProcessNoShowFollowUpsAsync(now, cancellationToken);
    }

    private async Task ProcessWindowAsync(
        DateTimeOffset now,
        TimeSpan leadTime,
        string windowKey,
        CancellationToken cancellationToken)
    {
        var target = now + leadTime;
        var from = target - WindowHalfWidth;
        var to = target + WindowHalfWidth;

        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Where(x =>
                x.Status == BookingStatus.Confirmed
                && x.StartAt >= from
                && x.StartAt <= to)
            .Select(x => new
            {
                x.Id,
                x.TenantId,
                x.CustomerId,
                x.StartAt,
            })
            .ToListAsync(cancellationToken);

        foreach (var booking in bookings)
        {
            var tenant = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == booking.TenantId, cancellationToken);

            if (tenant is null)
            {
                continue;
            }

            var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
            if (!entitlements.BasicReminders)
            {
                continue;
            }

            if (!entitlements.ConfigurableReminders && windowKey != "24h" && windowKey != "2h")
            {
                continue;
            }

            var contact = await recipientResolver.ResolvePlatformCustomerAsync(booking.CustomerId, cancellationToken);
            if (contact.IsFailure)
            {
                continue;
            }

            var dedupe = $"appointment-reminder:{booking.Id:D}:{windowKey}";
            var templateId = windowKey == "24h"
                ? NotificationTemplateIds.AppointmentReminder24h
                : NotificationTemplateIds.AppointmentReminder2h;

            var data = new Dictionary<string, string>
            {
                ["startAtUtc"] = booking.StartAt.ToString("o"),
            };

            if (!string.IsNullOrWhiteSpace(contact.Value!.Email))
            {
                await notificationDispatcher.SendAsync(
                    NotificationMessage.FromTemplate(
                        "email",
                        contact.Value.Email.Trim(),
                        templateId,
                        data,
                        booking.TenantId,
                        dedupe,
                        booking.CustomerId),
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(contact.Value.PhoneE164))
            {
                await notificationDispatcher.SendAsync(
                    NotificationMessage.FromTemplate(
                        "sms",
                        contact.Value.PhoneE164.Trim(),
                        templateId,
                        data,
                        booking.TenantId,
                        dedupe + ":sms",
                        booking.CustomerId),
                    cancellationToken);
            }
        }
    }

    private async Task ProcessNoShowFollowUpsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var graceAfterEnd = options.Value.Reminders.NoShowGraceAfterEnd;
        var cutoffEnd = now - graceAfterEnd;
        var earliestEnd = now.AddDays(-7);

        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Where(x =>
                x.Status == BookingStatus.Confirmed
                && x.EndAt <= cutoffEnd
                && x.EndAt >= earliestEnd)
            .Select(x => new { x.Id, x.TenantId, x.CustomerId, x.StartAt })
            .ToListAsync(cancellationToken);

        foreach (var booking in bookings)
        {
            var tenant = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == booking.TenantId, cancellationToken);

            if (tenant is null)
            {
                continue;
            }

            var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
            if (!entitlements.FollowUpAutomation)
            {
                continue;
            }

            var contact = await recipientResolver.ResolvePlatformCustomerAsync(booking.CustomerId, cancellationToken);
            if (contact.IsFailure || string.IsNullOrWhiteSpace(contact.Value!.Email))
            {
                continue;
            }

            var dedupe = $"appointment-followup:{booking.Id:D}:no-show";
            var data = new Dictionary<string, string>
            {
                ["startAtUtc"] = booking.StartAt.ToString("o"),
            };

            await notificationDispatcher.SendAsync(
                NotificationMessage.FromTemplate(
                    "email",
                    contact.Value.Email.Trim(),
                    NotificationTemplateIds.AppointmentFollowupNoShow,
                    data,
                    booking.TenantId,
                    dedupe,
                    booking.CustomerId),
                cancellationToken);
        }
    }
}
