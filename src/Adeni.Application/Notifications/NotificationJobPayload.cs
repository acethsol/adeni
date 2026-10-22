namespace Adeni.Application.Notifications;

/// <summary>Serializable notification send payload for background jobs.</summary>
public sealed record NotificationJobPayload(
    string Channel,
    string RecipientKey,
    string Subject,
    string Body,
    Guid? TenantId,
    string? DedupeEventId,
    PaymentReceiptNotificationJobArgs? PaymentReceipt = null,
    string? TemplateId = null,
    IReadOnlyDictionary<string, string>? TemplateData = null,
    Guid? RecipientCustomerId = null,
    string? LocaleHint = null)
{
    public static NotificationJobPayload FromMessage(NotificationMessage message) =>
        new(
            message.Channel,
            message.RecipientKey,
            message.Subject,
            message.Body,
            message.TenantId,
            message.DedupeEventId,
            message.PaymentReceipt,
            message.TemplateId,
            message.TemplateData,
            message.RecipientCustomerId,
            message.LocaleHint);

    public NotificationMessage ToMessage() =>
        new(
            Channel,
            RecipientKey,
            Subject,
            Body,
            TenantId,
            DedupeEventId,
            PaymentReceipt: PaymentReceipt,
            TemplateId: TemplateId,
            TemplateData: TemplateData,
            RecipientCustomerId: RecipientCustomerId,
            LocaleHint: LocaleHint);
}
