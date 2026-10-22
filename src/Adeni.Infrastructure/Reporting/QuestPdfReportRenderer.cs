namespace Adeni.Infrastructure.Reporting;

using Adeni.Application.Reporting;
using Adeni.Domain.Common;

public sealed class QuestPdfReportRenderer : IReportRenderer
{
    public Task<Result<byte[]>> RenderDocumentAsync(
        ReportKind kind,
        BillingDocumentReportData data,
        CancellationToken cancellationToken = default)
    {
        if (data.Kind != kind)
        {
            return Task.FromResult(Result.Failure<byte[]>(
                Error.Validation("Report kind does not match document data.")));
        }

        return kind switch
        {
            ReportKind.BillingReceipt or ReportKind.BillingInvoice =>
                Task.FromResult(Result.Success(BillingDocumentQuestPdfTemplate.Render(data))),
            _ => Task.FromResult(Result.Failure<byte[]>(Error.Validation("Unsupported report kind."))),
        };
    }
}
