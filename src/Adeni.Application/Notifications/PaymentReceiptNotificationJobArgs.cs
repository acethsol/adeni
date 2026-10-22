namespace Adeni.Application.Notifications;

public sealed record PaymentReceiptNotificationJobArgs(
    Guid PaymentIntentId,
    Guid TenantId,
    Guid? BookingId,
    decimal Amount,
    string Currency);
