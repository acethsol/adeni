namespace Adeni.Application.Reporting;

public sealed record StatementDocumentReportData(
    string BusinessName,
    string CustomerDisplayName,
    string CustomerPhoneMasked,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<CustomerStatementLine> Lines) : IReportDocumentData
{
    public ReportKind Kind => ReportKind.CustomerStatement;
}
