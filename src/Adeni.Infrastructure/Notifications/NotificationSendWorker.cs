namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Adeni.Application.Security;
using Microsoft.Extensions.Logging;

public sealed class NotificationSendWorker(
    INotificationDeliveryDispatcher deliveryDispatcher,
    PaymentReceiptNotificationSender paymentReceiptSender,
    NotificationTemplateDeliveryComposer templateComposer,
    INotificationLocaleResolver localeResolver,
    ILogger<NotificationSendWorker> logger)
{
    public async Task SendAsync(NotificationJobPayload payload, CancellationToken cancellationToken = default)
    {
        if (payload.PaymentReceipt is { } receiptArgs)
        {
            logger.LogInformation(
                "Notification job payment receipt paymentIntent={PaymentIntentId} tenant={TenantId}",
                receiptArgs.PaymentIntentId,
                receiptArgs.TenantId);
            await paymentReceiptSender.SendAsync(receiptArgs, cancellationToken);
            return;
        }

        logger.LogInformation(
            "Notification job send channel={Channel} dedupe={DedupeEventId} recipient={Recipient}",
            payload.Channel,
            payload.DedupeEventId,
            MaskRecipient(payload.Channel, payload.RecipientKey));

        var message = payload.ToMessage();
        if (!string.IsNullOrWhiteSpace(payload.TemplateId))
        {
            var locale = await localeResolver.ResolveAsync(
                payload.TenantId,
                payload.RecipientCustomerId,
                payload.LocaleHint,
                cancellationToken);

            var data = payload.TemplateData ?? new Dictionary<string, string>();
            var rendered = await templateComposer.RenderForDeliveryAsync(
                payload.Channel,
                payload.TemplateId,
                payload.TenantId,
                locale,
                data,
                cancellationToken);

            if (rendered.IsFailure)
            {
                logger.LogWarning(
                    "Skipping notification job template={TemplateId}: {Error}",
                    payload.TemplateId,
                    rendered.Error.Message);
                return;
            }

            message = message with
            {
                Subject = rendered.Value!.Subject,
                Body = rendered.Value.Body,
            };
        }

        await deliveryDispatcher.SendAsync(message, cancellationToken);
    }

    private static string MaskRecipient(string channel, string recipientKey) =>
        channel.Trim().ToLowerInvariant() switch
        {
            "email" => PiiMasker.MaskEmail(recipientKey),
            "sms" or "whatsapp" => PiiMasker.MaskPhone(recipientKey),
            _ => PiiMasker.MaskEmail(recipientKey),
        };
}
