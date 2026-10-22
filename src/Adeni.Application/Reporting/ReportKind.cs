namespace Adeni.Application.Reporting;

/// <summary>Document templates supported by <see cref="IReportRenderer"/> (39A: billing receipts/invoices).</summary>
public enum ReportKind
{
    BillingReceipt = 0,
    BillingInvoice = 1,
    Quote = 2,
    CustomerStatement = 3,
}
