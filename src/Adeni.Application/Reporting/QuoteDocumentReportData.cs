namespace Adeni.Application.Reporting;

public sealed record QuoteDocumentReportData(
    string BusinessName,
    string QuoteId,
    string Description,
    string? ServiceAddress,
    decimal? QuotedAmount,
    string? QuotedCurrency,
    string? QuoteNotes,
    DateTimeOffset? ProposedStartAt,
    DateTimeOffset? ProposedEndAt,
    DateTimeOffset? ExpiresAt,
    string Status,
    byte[]? BusinessLogoBytes = null) : IReportDocumentData
{
    public ReportKind Kind => ReportKind.Quote;
}
