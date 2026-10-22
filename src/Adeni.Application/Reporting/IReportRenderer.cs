namespace Adeni.Application.Reporting;

using Adeni.Domain.Common;

public interface IReportRenderer
{
    Task<Result<byte[]>> RenderDocumentAsync(
        ReportKind kind,
        IReportDocumentData data,
        CancellationToken cancellationToken = default);
}
