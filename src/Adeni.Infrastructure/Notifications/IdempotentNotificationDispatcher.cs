namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Adeni.Domain.Notifications;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class IdempotentNotificationDispatcher(
    AdeniDbContext dbContext,
    ChannelNotificationDispatcher inner,
    ILogger<IdempotentNotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.DedupeEventId))
        {
            await inner.SendAsync(message, cancellationToken);
            return;
        }

        var dedupeKey = BuildDedupeKey(message);
        var existing = await dbContext.NotificationDeliveries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DedupeKey == dedupeKey, cancellationToken);

        if (existing is not null)
        {
            logger.LogDebug(
                "Skipping duplicate notification {DedupeKey} (status {Status}).",
                dedupeKey,
                existing.Status);
            return;
        }

        var delivery = new NotificationDelivery
        {
            Id = Guid.NewGuid(),
            DedupeKey = dedupeKey,
            TenantId = message.TenantId,
            Channel = message.Channel.Trim().ToLowerInvariant(),
            RecipientKey = message.RecipientKey,
            Status = NotificationDeliveryStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        dbContext.NotificationDeliveries.Add(delivery);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            logger.LogDebug("Duplicate notification delivery race for {DedupeKey}.", dedupeKey);
            return;
        }

        try
        {
            await inner.SendAsync(message, cancellationToken);
            delivery.Status = NotificationDeliveryStatus.Sent;
            delivery.CompletedAt = DateTimeOffset.UtcNow;
        }
        catch
        {
            delivery.Status = NotificationDeliveryStatus.Failed;
            delivery.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static string BuildDedupeKey(NotificationMessage message) =>
        $"{message.DedupeEventId}|{message.Channel.Trim().ToLowerInvariant()}|{message.RecipientKey.Trim().ToLowerInvariant()}";
}
