namespace Adeni.Infrastructure.Reporting;

using Adeni.Application.Reporting;
using Adeni.Domain.Common;

public sealed class QuestPdfReportRenderer : IReportRenderer
{
    public Task<Result<byte[]>> RenderDocumentAsync(
        ReportKind kind,
        IReportDocumentData data,
        CancellationToken cancellationToken = default)
    {
        if (data.Kind != kind)
        {
            return Task.FromResult(Result.Failure<byte[]>(
                Error.Validation("Report kind does not match document data.")));
        }

        return kind switch
        {
            ReportKind.BillingReceipt or ReportKind.BillingInvoice when data is BillingDocumentReportData billing =>
                Task.FromResult(Result.Success(BillingDocumentQuestPdfTemplate.Render(billing))),
            ReportKind.Quote when data is QuoteDocumentReportData quote =>
                Task.FromResult(Result.Success(QuoteStatementQuestPdfTemplate.RenderQuote(quote))),
            ReportKind.CustomerStatement when data is StatementDocumentReportData statement =>
                Task.FromResult(Result.Success(QuoteStatementQuestPdfTemplate.RenderStatement(statement))),
            _ => Task.FromResult(Result.Failure<byte[]>(Error.Validation("Unsupported report kind."))),
        };
    }
}
