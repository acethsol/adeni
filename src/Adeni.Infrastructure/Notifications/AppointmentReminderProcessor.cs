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
            var subject = windowKey == "24h" ? "Appointment reminder (24 hours)" : "Appointment reminder (2 hours)";
            var body =
                $"Reminder: your appointment is scheduled for {booking.StartAt:u}.";

            if (!string.IsNullOrWhiteSpace(contact.Value!.Email))
            {
                await notificationDispatcher.SendAsync(
                    new NotificationMessage(
                        "email",
                        contact.Value.Email.Trim(),
                        subject,
                        body,
                        booking.TenantId,
                        dedupe),
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(contact.Value.PhoneE164))
            {
                await notificationDispatcher.SendAsync(
                    new NotificationMessage(
                        "sms",
                        contact.Value.PhoneE164.Trim(),
                        subject,
                        body,
                        booking.TenantId,
                        dedupe + ":sms"),
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
            await notificationDispatcher.SendAsync(
                new NotificationMessage(
                    "email",
                    contact.Value.Email.Trim(),
                    "We missed you — book again",
                    $"We noticed you may have missed your appointment on {booking.StartAt:u}. " +
                    "Reply or visit the business profile to pick a new time.",
                    booking.TenantId,
                    dedupe),
                cancellationToken);
        }
    }
}
