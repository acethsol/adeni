namespace Adeni.Application.Notifications;

public interface INotificationDispatcher
{
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}

public sealed record NotificationMessage(
    string Channel,
    string RecipientKey,
    string Subject,
    string Body,
    Guid? TenantId = null,
    string? DedupeEventId = null,
    IReadOnlyList<EmailAttachment>? EmailAttachments = null,
    PaymentReceiptNotificationJobArgs? PaymentReceipt = null,
    string? TemplateId = null,
    IReadOnlyDictionary<string, string>? TemplateData = null,
    Guid? RecipientCustomerId = null,
    string? LocaleHint = null)
{
    public static NotificationMessage FromTemplate(
        string channel,
        string recipientKey,
        string templateId,
        IReadOnlyDictionary<string, string> templateData,
        Guid? tenantId,
        string dedupeEventId,
        Guid? recipientCustomerId = null,
        string? localeHint = null,
        IReadOnlyList<EmailAttachment>? emailAttachments = null) =>
        new(
            channel,
            recipientKey,
            Subject: string.Empty,
            Body: string.Empty,
            tenantId,
            dedupeEventId,
            emailAttachments,
            PaymentReceipt: null,
            templateId,
            templateData,
            recipientCustomerId,
            localeHint);
}
