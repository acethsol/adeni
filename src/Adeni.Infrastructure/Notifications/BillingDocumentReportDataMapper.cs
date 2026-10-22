namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Finance;
using Adeni.Application.Reporting;

internal static class BillingDocumentReportDataMapper
{
    public static BillingDocumentReportData ToReportData(BillingDocumentResponse document)
    {
        var kind = string.Equals(document.Kind, "Receipt", StringComparison.OrdinalIgnoreCase)
            ? ReportKind.BillingReceipt
            : ReportKind.BillingInvoice;

        return new BillingDocumentReportData(
            kind,
            document.DocumentNumber,
            document.BusinessName,
            document.CustomerName,
            document.Currency,
            document.Total,
            document.Method,
            document.IssuedAt,
            document.Lines
                .Select(x => new BillingDocumentLineReportData(
                    x.Description,
                    x.Quantity,
                    x.UnitAmount,
                    x.LineTotal,
                    x.SortOrder))
                .ToArray());
    }
}
