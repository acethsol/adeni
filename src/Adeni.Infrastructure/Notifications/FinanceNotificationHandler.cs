namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Events;
using Adeni.Application.Finance;
using Adeni.Application.Notifications;
using Adeni.Application.Reporting;
using Adeni.Domain.Finance.Events;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class FinanceNotificationHandler(
    INotificationDispatcher notificationDispatcher,
    INotificationRecipientResolver recipientResolver,
    IBillingDocumentService billingDocuments,
    IReportRenderer reportRenderer,
    AdeniDbContext dbContext,
    ILogger<FinanceNotificationHandler> logger) :
    IDomainEventHandler<PaymentCompleted>,
    IDomainEventHandler<PaymentFailed>
{
    public async Task HandleAsync(PaymentCompleted domainEvent, CancellationToken cancellationToken = default)
    {
        var receipt = await billingDocuments.IssueReceiptForPaymentAsync(
            domainEvent.PaymentIntentId,
            cancellationToken);

        if (receipt.IsFailure)
        {
            logger.LogWarning(
                "Skipping payment receipt email for {PaymentIntentId}: {Error}",
                domainEvent.PaymentIntentId,
                receipt.Error.Message);
            return;
        }

        var toEmail = receipt.Value!.CustomerEmail;
        if (string.IsNullOrWhiteSpace(toEmail) && domainEvent.BookingId is { } bookingId)
        {
            var contact = await recipientResolver.ResolveBookingCustomerAsync(
                domainEvent.TenantId,
                bookingId,
                cancellationToken);
            toEmail = contact.IsSuccess ? contact.Value!.Email : null;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            var payment = await dbContext.PaymentIntents
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == domainEvent.PaymentIntentId, cancellationToken);
            toEmail = payment?.CustomerEmail;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            logger.LogDebug(
                "Skipping payment receipt email for {PaymentIntentId} — no customer email.",
                domainEvent.PaymentIntentId);
            return;
        }

        var reportData = BillingDocumentReportDataMapper.ToReportData(receipt.Value);
        var pdf = await reportRenderer.RenderDocumentAsync(
            reportData.Kind,
            reportData,
            cancellationToken);

        if (pdf.IsFailure)
        {
            logger.LogWarning(
                "Skipping payment receipt attachment for {PaymentIntentId}: {Error}",
                domainEvent.PaymentIntentId,
                pdf.Error.Message);
            return;
        }

        var fileName = $"receipt-{receipt.Value.DocumentNumber}.pdf";
        var body =
            $"<p>Thank you — your payment of {domainEvent.Amount} {domainEvent.Currency} was received.</p>" +
            $"<p>Your receipt ({receipt.Value.DocumentNumber}) is attached.</p>";

        await notificationDispatcher.SendAsync(
            new NotificationMessage(
                "email",
                toEmail.Trim(),
                "Payment receipt",
                body,
                domainEvent.TenantId,
                $"payment-receipt:{domainEvent.PaymentIntentId:D}",
                [new EmailAttachment(fileName, "application/pdf", pdf.Value!)]),
            cancellationToken);
    }

    public Task HandleAsync(PaymentFailed domainEvent, CancellationToken cancellationToken = default) =>
        notificationDispatcher.SendAsync(
            new NotificationMessage(
                "email",
                domainEvent.TenantId.ToString(),
                "Payment failed",
                $"Payment {domainEvent.PaymentIntentId} failed.{(string.IsNullOrWhiteSpace(domainEvent.Reason) ? "" : $" Reason: {domainEvent.Reason}")}",
                domainEvent.TenantId,
                $"payment-failed:{domainEvent.PaymentIntentId:D}"),
            cancellationToken);
}
