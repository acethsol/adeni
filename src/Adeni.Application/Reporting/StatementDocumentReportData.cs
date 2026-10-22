namespace Adeni.Application.Reporting;

public sealed record StatementDocumentReportData(
    string BusinessName,
    string CustomerDisplayName,
    string CustomerPhoneMasked,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<CustomerStatementLine> Lines,
    byte[]? BusinessLogoBytes = null) : IReportDocumentData
{
    public ReportKind Kind => ReportKind.CustomerStatement;
}
