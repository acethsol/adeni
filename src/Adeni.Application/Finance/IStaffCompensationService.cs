namespace Adeni.Application.Finance;

using Adeni.Domain.Common;

public enum CompensationExportFormat
{
    Csv,
    Json,
}

public sealed record StaffCompensationExportRow(
    DateTimeOffset CreatedAt,
    Guid StaffMemberId,
    string StaffDisplayName,
    string Type,
    decimal Amount,
    string Currency,
    Guid? BookingId,
    Guid? PaymentIntentId);

public interface IStaffCompensationService
{
    Task<Result> RecordTipForTillPaymentAsync(
        Guid tenantId,
        Guid bookingId,
        decimal tipAmount,
        Guid? staffMemberId,
        string currency,
        CancellationToken cancellationToken = default);

    Task<Result> RecordTipForPaymentIntentAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default);

    Task<Result> AccrueCommissionForPaidBookingAsync(
        Guid tenantId,
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task<Result<StaffCompensationExportPayload>> ExportAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CompensationExportFormat format,
        CancellationToken cancellationToken = default);
}

public sealed record StaffCompensationExportPayload(
    string ContentType,
    string FileName,
    byte[] Body);
