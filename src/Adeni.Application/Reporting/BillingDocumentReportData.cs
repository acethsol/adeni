namespace Adeni.Application.Reporting;

public sealed record BillingDocumentLineReportData(
    string Description,
    decimal Quantity,
    decimal UnitAmount,
    decimal LineTotal,
    int SortOrder);

/// <summary>Snapshot fields for QuestPDF billing document templates (no live tenant/profile reads).</summary>
public sealed record BillingDocumentReportData(
    ReportKind Kind,
    string DocumentNumber,
    string BusinessNameSnapshot,
    string? CustomerNameSnapshot,
    string Currency,
    decimal Total,
    string? MethodLabel,
    DateTimeOffset? IssuedAt,
    IReadOnlyList<BillingDocumentLineReportData> Lines);
