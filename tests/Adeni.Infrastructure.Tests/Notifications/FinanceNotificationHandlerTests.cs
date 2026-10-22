namespace Adeni.Infrastructure.Tests.Notifications;

using Adeni.Application.Events;
using Adeni.Application.Finance;
using Adeni.Application.Notifications;
using Adeni.Application.Reporting;
using Adeni.Domain.Common;
using Adeni.Domain.Finance.Events;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Notifications;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class FinanceNotificationHandlerTests
{
    [Fact]
    public async Task PaymentCompleted_uses_IReportRenderer_and_attaches_pdf()
    {
        var tenantId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var renderer = new CapturingReportRenderer();
        var billing = new StubBillingDocuments(paymentIntentId, tenantId);
        await using var provider = NotificationDispatcherTests.BuildProvider(services =>
        {
            services.AddSingleton<IReportRenderer>(renderer);
            services.AddSingleton<IBillingDocumentService>(billing);
            services.AddScoped<INotificationRecipientResolver, NotificationRecipientResolver>();
            services.AddScoped<FinanceNotificationHandler>();
        });
        using var handlerScope = provider.CreateScope();
        var handler = handlerScope.ServiceProvider.GetRequiredService<FinanceNotificationHandler>();

        await handler.HandleAsync(
            new PaymentCompleted(
                paymentIntentId,
                tenantId,
                null,
                Domain.Finance.PaymentIntentType.Link,
                50m,
                "NGN",
                "ref",
                DateTimeOffset.UtcNow));

        var sink = handlerScope.ServiceProvider.GetRequiredService<NotificationDispatcherTests.CapturingEmailProvider>();
        Assert.Single(sink.Sent);
        Assert.NotNull(sink.Sent[0].Attachments);
        Assert.Single(sink.Sent[0].Attachments!);
        Assert.Equal(1, renderer.RenderCalls);
    }

    private sealed class CapturingReportRenderer : IReportRenderer
    {
        public int RenderCalls { get; private set; }

        public Task<Result<byte[]>> RenderDocumentAsync(
            ReportKind kind,
            BillingDocumentReportData data,
            CancellationToken cancellationToken = default)
        {
            RenderCalls++;
            return Task.FromResult(Result.Success(new byte[] { 0x25, 0x50, 0x44, 0x46 }));
        }
    }

    private sealed class StubBillingDocuments(Guid paymentIntentId, Guid tenantId) : IBillingDocumentService
    {
        public Task<Result<BillingDocumentResponse>> IssueReceiptForPaymentAsync(
            Guid pid,
            CancellationToken cancellationToken = default)
        {
            if (pid != paymentIntentId)
            {
                return Task.FromResult(Result.Failure<BillingDocumentResponse>(Error.NotFound("Document")));
            }

            return Task.FromResult(Result.Success(new BillingDocumentResponse(
                Guid.NewGuid(),
                tenantId,
                "Receipt",
                "Issued",
                "R-1001",
                null,
                paymentIntentId,
                "Test Salon",
                "Guest",
                "guest@example.com",
                "NGN",
                50m,
                null,
                50m,
                "Online",
                DateTimeOffset.UtcNow,
                [],
                "https://example.com/r",
                "token")));
        }

        public Task<Result<BillingDocumentResponse>> GetAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<BillingDocumentResponse>> GetByPublicTokenAsync(string token, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<IReadOnlyList<BillingDocumentResponse>>> ListForTenantAsync(
            Guid tenantId,
            string? kind = null,
            Guid? bookingId = null,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<BillingDocumentResponse>> CreateInvoiceAsync(
            CreateInvoiceRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<BillingDocumentResponse>> VoidAsync(
            Guid tenantId,
            Guid documentId,
            VoidBillingDocumentRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<byte[]>> GetPdfAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<BillingDocumentResponse>> EmailAsync(
            Guid tenantId,
            Guid documentId,
            EmailBillingDocumentRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<BillingDocumentResponse>> GetReceiptForPaymentAsync(
            Guid pid,
            CancellationToken cancellationToken = default) =>
            IssueReceiptForPaymentAsync(pid, cancellationToken);

        public Task<Result<BillingDocumentResponse>> IssueReceiptForTillPaymentAsync(
            Guid tenantId,
            Guid bookingId,
            string? externalReference = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
