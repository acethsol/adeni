namespace Adeni.Infrastructure.Tests.Notifications;

using Adeni.Application.Finance;
using Adeni.Application.Reporting;
using Adeni.Domain.Common;

internal sealed class NotificationDispatchTestBillingDocuments : IBillingDocumentService
{
    public Task<Result<BillingDocumentResponse>> IssueReceiptForPaymentAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure<BillingDocumentResponse>(Error.NotFound("Document")));

    public Task<Result<BillingDocumentResponse>> GetAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<BillingDocumentResponse>> GetByPublicTokenAsync(string token, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<IReadOnlyList<BillingDocumentResponse>>> ListForTenantAsync(
        Guid tenantId,
        string? kind = null,
        Guid? bookingId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<IReadOnlyList<BillingDocumentResponse>>> ListForCustomerAsync(
        string customerAuth0Sub,
        string? kind = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success<IReadOnlyList<BillingDocumentResponse>>(Array.Empty<BillingDocumentResponse>()));

    public Task<Result<BillingDocumentResponse>> GetForCustomerAsync(
        string customerAuth0Sub,
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure<BillingDocumentResponse>(Error.NotFound("Document")));

    public Task<Result<BillingDocumentResponse>> CreateInvoiceAsync(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<BillingDocumentResponse>> VoidAsync(
        Guid tenantId,
        Guid documentId,
        VoidBillingDocumentRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<byte[]>> GetPdfAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<BillingDocumentResponse>> EmailAsync(
        Guid tenantId,
        Guid documentId,
        EmailBillingDocumentRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<BillingDocumentResponse>> GetReceiptForPaymentAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default) =>
        IssueReceiptForPaymentAsync(paymentIntentId, cancellationToken);

    public Task<Result<BillingDocumentResponse>> IssueReceiptForTillPaymentAsync(
        Guid tenantId,
        Guid bookingId,
        string? externalReference = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class NotificationDispatchTestReportRenderer : IReportRenderer
{
    public Task<Result<byte[]>> RenderDocumentAsync(
        ReportKind kind,
        IReportDocumentData data,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(Array.Empty<byte>()));
}
