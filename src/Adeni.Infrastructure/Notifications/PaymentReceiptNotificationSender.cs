namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Finance;
using Adeni.Application.Notifications;
using Adeni.Application.Reporting;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class PaymentReceiptNotificationSender(
    INotificationDeliveryDispatcher deliveryDispatcher,
    INotificationRecipientResolver recipientResolver,
    INotificationTemplateRenderer templateRenderer,
    INotificationLocaleResolver localeResolver,
    IBillingDocumentService billingDocuments,
    IReportRenderer reportRenderer,
    AdeniDbContext dbContext,
    ILogger<PaymentReceiptNotificationSender> logger)
{
    public async Task SendAsync(
        PaymentReceiptNotificationJobArgs args,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Payment receipt notification job paymentIntent={PaymentIntentId} tenant={TenantId}",
            args.PaymentIntentId,
            args.TenantId);

        var receipt = await billingDocuments.IssueReceiptForPaymentAsync(
            args.PaymentIntentId,
            cancellationToken);

        if (receipt.IsFailure)
        {
            logger.LogWarning(
                "Skipping payment receipt email for {PaymentIntentId}: {Error}",
                args.PaymentIntentId,
                receipt.Error.Message);
            return;
        }

        var toEmail = receipt.Value!.CustomerEmail;
        if (string.IsNullOrWhiteSpace(toEmail) && args.BookingId is { } bookingId)
        {
            var contact = await recipientResolver.ResolveBookingCustomerAsync(
                args.TenantId,
                bookingId,
                cancellationToken);
            toEmail = contact.IsSuccess ? contact.Value!.Email : null;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            var payment = await dbContext.PaymentIntents
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == args.PaymentIntentId, cancellationToken);
            toEmail = payment?.CustomerEmail;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            logger.LogDebug(
                "Skipping payment receipt email for {PaymentIntentId} — no customer email.",
                args.PaymentIntentId);
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
                args.PaymentIntentId,
                pdf.Error.Message);
            return;
        }

        var fileName = $"receipt-{receipt.Value.DocumentNumber}.pdf";

        Guid? customerId = null;
        if (args.BookingId is { } bookingIdForCustomer)
        {
            customerId = await dbContext.Bookings
                .AsNoTracking()
                .Where(x => x.Id == bookingIdForCustomer)
                .Select(x => (Guid?)x.CustomerId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var locale = await localeResolver.ResolveAsync(args.TenantId, customerId, null, cancellationToken);
        var templateData = new Dictionary<string, string>
        {
            ["amount"] = args.Amount.ToString("0.00"),
            ["currency"] = args.Currency,
            ["documentNumber"] = receipt.Value.DocumentNumber,
        };

        var rendered = await templateRenderer.RenderAsync(
            NotificationTemplateIds.PaymentReceipt,
            args.TenantId,
            locale,
            templateData,
            cancellationToken);

        if (rendered.IsFailure)
        {
            logger.LogWarning(
                "Skipping payment receipt email for {PaymentIntentId}: {Error}",
                args.PaymentIntentId,
                rendered.Error.Message);
            return;
        }

        await deliveryDispatcher.SendAsync(
            new NotificationMessage(
                "email",
                toEmail.Trim(),
                rendered.Value!.Subject,
                rendered.Value.Body,
                args.TenantId,
                $"payment-receipt:{args.PaymentIntentId:D}",
                [new EmailAttachment(fileName, "application/pdf", pdf.Value!)]),
            cancellationToken);
    }
}
