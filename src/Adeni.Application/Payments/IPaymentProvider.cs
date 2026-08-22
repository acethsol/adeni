namespace Adeni.Application.Payments;

using Adeni.Domain.Common;

public sealed record InitializePaymentRequest(
    Guid TenantId,
    Guid? BookingId,
    decimal Amount,
    string Currency);

public sealed record PaymentIntentResponse(
    Guid Id,
    Guid TenantId,
    Guid? BookingId,
    decimal Amount,
    string Currency,
    string Status,
    string CheckoutUrl,
    string ProviderReference);

public interface IPaymentProvider
{
    Task<Result<PaymentIntentResponse>> InitializeAsync(
        InitializePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentIntentResponse>> GetAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default);
}
