namespace Adeni.Application.Payments;

using Adeni.Domain.Common;

public sealed record BillingDocumentLineResponse(
    string Description,
    decimal Quantity,
    decimal UnitAmount,
    decimal LineTotal,
    int SortOrder);

public sealed record BillingDocumentResponse(
    Guid Id,
    Guid TenantId,
    string Kind,
    string Status,
    string DocumentNumber,
    Guid? BookingId,
    Guid? PaymentIntentId,
    string BusinessName,
    string? CustomerName,
    string? CustomerEmail,
    string Currency,
    decimal Subtotal,
    decimal? TaxTotal,
    decimal Total,
    string? Method,
    DateTimeOffset? IssuedAt,
    IReadOnlyList<BillingDocumentLineResponse> Lines,
    string PublicUrl,
    string PublicToken);

public sealed record CreateInvoiceRequest(
    Guid TenantId,
    Guid? BookingId = null,
    decimal? Amount = null,
    string? Currency = null,
    string? Description = null,
    string? CustomerEmail = null,
    bool Issue = true,
    bool InitializePayment = true);

public sealed record VoidBillingDocumentRequest(string? Reason = null);

public sealed record EmailBillingDocumentRequest(string? ToEmail = null);

public interface IBillingDocumentService
{
    Task<Result<BillingDocumentResponse>> GetAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<Result<BillingDocumentResponse>> GetByPublicTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<BillingDocumentResponse>>> ListForTenantAsync(
        Guid tenantId,
        string? kind = null,
        Guid? bookingId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<Result<BillingDocumentResponse>> CreateInvoiceAsync(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<BillingDocumentResponse>> VoidAsync(
        Guid tenantId,
        Guid documentId,
        VoidBillingDocumentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<byte[]>> GetPdfAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<Result<BillingDocumentResponse>> EmailAsync(
        Guid tenantId,
        Guid documentId,
        EmailBillingDocumentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<BillingDocumentResponse>> GetReceiptForPaymentAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default);

    /// <summary>Idempotent: one receipt per completed payment intent.</summary>
    Task<Result<BillingDocumentResponse>> IssueReceiptForPaymentAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default);

    /// <summary>Idempotent per booking when paid externally at till.</summary>
    Task<Result<BillingDocumentResponse>> IssueReceiptForTillPaymentAsync(
        Guid tenantId,
        Guid bookingId,
        string? externalReference = null,
        CancellationToken cancellationToken = default);
}
