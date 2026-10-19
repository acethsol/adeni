namespace Adeni.Application.Finance;

using Adeni.Application.Common;
using Adeni.Domain.Common;
using Adeni.Domain.Finance;

public sealed record InitializePaymentRequest(
    Guid TenantId,
    Guid? BookingId,
    decimal? Amount,
    string Currency,
    string? Type = null,
    string? Description = null,
    string? CustomerEmail = null,
    string? CallbackUrl = null,
    decimal? TipAmount = null,
    Guid? TipStaffMemberId = null);

public sealed record CreatePaymentLinkRequest(
    Guid TenantId,
    decimal Amount,
    string Currency,
    string Description,
    string? CustomerEmail = null,
    string? CallbackUrl = null);

public sealed record RefundPaymentRequest(
    Guid TenantId,
    Guid PaymentIntentId,
    decimal? Amount = null,
    string? Reason = null);

public sealed record PaymentIntentResponse(
    Guid Id,
    Guid TenantId,
    Guid? BookingId,
    string Type,
    decimal Amount,
    decimal PlatformFeeAmount,
    string Currency,
    string Status,
    string CheckoutUrl,
    string ProviderReference,
    string? Description = null,
    DateTimeOffset? CreatedAt = null);

public sealed record PaymentLedgerEntry(
    Guid Id,
    Guid TenantId,
    Guid? BookingId,
    string Type,
    decimal Amount,
    decimal PlatformFeeAmount,
    string Currency,
    string Status,
    string ProviderReference,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PaymentWebhookResult(bool Processed, string? Message = null);

public sealed record PaymentReceiptResponse(
    Guid PaymentIntentId,
    Guid? BookingId,
    string BusinessName,
    string? Description,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset? PaidAt,
    string Method,
    string ProviderReference);

public interface IPaymentProvider
{
    string ProviderName { get; }

    Task<Result<ProviderInitializeResult>> InitializeAsync(
        ProviderInitializeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ProviderPaymentStatus>> GetProviderStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentWebhookPayload>> ParseWebhookAsync(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);

    Task<Result<ProviderRefundResult>> RefundAsync(
        string providerReference,
        decimal? amount,
        CancellationToken cancellationToken = default);
}

public sealed record ProviderInitializeRequest(
    Guid PaymentIntentId,
    Guid TenantId,
    Guid? BookingId,
    decimal Amount,
    string Currency,
    PaymentIntentType Type,
    string ProviderReference,
    string? Description,
    string? CustomerEmail,
    string? CallbackUrl,
    decimal PlatformFeeAmount,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record ProviderInitializeResult(
    string CheckoutUrl,
    string ProviderReference);

public sealed record ProviderPaymentStatus(
    string ProviderReference,
    bool IsSuccessful,
    bool IsFailed,
    string? FailureReason);

public sealed record PaymentWebhookPayload(
    string EventType,
    string ProviderReference,
    bool IsSuccessful,
    bool IsFailed,
    bool IsRefund,
    string? FailureReason,
    decimal? Amount,
    string? Currency);

public sealed record ProviderRefundResult(
    string ProviderReference,
    decimal Amount,
    string Status);

public interface IPaymentOrchestrator
{
    Task<Result<PaymentIntentResponse>> InitializeAsync(
        InitializePaymentRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentIntentResponse>> CreatePaymentLinkAsync(
        CreatePaymentLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentIntentResponse>> GetAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResult<PaymentLedgerEntry>>> ListLedgerAsync(
        Guid tenantId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentWebhookResult>> ProcessWebhookAsync(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentIntentResponse>> ConfirmStubCheckoutAsync(
        string providerReference,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentIntentResponse>> InitiateRefundAsync(
        RefundPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentReceiptResponse>> GetReceiptAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default);
}

public interface IPlatformFeeCalculator
{
    decimal CalculateFee(decimal amount, string marketId);
    decimal GetFeePercent(string marketId);
}
