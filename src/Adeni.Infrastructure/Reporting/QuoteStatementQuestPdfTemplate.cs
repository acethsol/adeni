namespace Adeni.Infrastructure.Reporting;

using Adeni.Application.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

internal static class QuoteStatementQuestPdfTemplate
{
    public static byte[] RenderQuote(QuoteDocumentReportData data) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11));
                page.Header().Element(header =>
                    QuestPdfBrandingLayout.ComposeBusinessHeader(
                        header,
                        data.BusinessName,
                        data.BusinessLogoBytes,
                        column => column.Item().Text("Quote")
                            .SemiBold()
                            .FontSize(14)
                            .FontColor(QuestPdfBrandColors.Purple)));
                page.Content().Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text($"Quote ID: {data.QuoteId}");
                    column.Item().Text($"Status: {data.Status}");
                    column.Item().Text($"Description: {data.Description}");
                    if (!string.IsNullOrWhiteSpace(data.ServiceAddress))
                    {
                        column.Item().Text($"Service address: {data.ServiceAddress}");
                    }

                    if (data.QuotedAmount is { } amount)
                    {
                        column.Item().Text($"Amount: {amount:0.##} {data.QuotedCurrency ?? string.Empty}").SemiBold();
                    }

                    if (!string.IsNullOrWhiteSpace(data.QuoteNotes))
                    {
                        column.Item().Text($"Notes: {data.QuoteNotes}");
                    }

                    if (data.ProposedStartAt is { } start)
                    {
                        column.Item().Text($"Proposed start: {start:u}");
                    }

                    if (data.ProposedEndAt is { } end)
                    {
                        column.Item().Text($"Proposed end: {end:u}");
                    }

                    if (data.ExpiresAt is { } expires)
                    {
                        column.Item().Text($"Expires: {expires:u}");
                    }
                });
                page.Footer().Element(QuestPdfBrandingLayout.ComposePoweredByFooter);
            });
        }).GeneratePdf();

    public static byte[] RenderStatement(StatementDocumentReportData data) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Element(header =>
                    QuestPdfBrandingLayout.ComposeBusinessHeader(
                        header,
                        data.BusinessName,
                        data.BusinessLogoBytes,
                        column =>
                        {
                            column.Item().Text("Customer statement")
                                .SemiBold()
                                .FontSize(14)
                                .FontColor(QuestPdfBrandColors.Purple);
                            column.Item().Text($"{data.CustomerDisplayName} · {data.CustomerPhoneMasked}");
                            column.Item().Text($"Period: {data.From:u} — {data.To:u}").FontSize(9);
                        }));
                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    foreach (var line in data.Lines)
                    {
                        var amount = line.Amount is { } value
                            ? $"{value:0.##} {line.Currency ?? string.Empty}".Trim()
                            : "—";
                        column.Item().Row(row =>
                        {
                            row.RelativeItem(2).Text(line.OccurredAt.ToString("u"));
                            row.RelativeItem(3).Text(line.Description);
                            row.RelativeItem(1).AlignRight().Text(amount);
                        });
                    }

                    if (data.Lines.Count == 0)
                    {
                        column.Item().Text("No activity in this period.");
                    }
                });
                page.Footer().Column(footer =>
                {
                    footer.Item().AlignCenter().Text("Activity summary — not a tax invoice or wallet balance.")
                        .FontSize(8);
                    footer.Item().PaddingTop(6).Element(QuestPdfBrandingLayout.ComposePoweredByFooter);
                });
            });
        }).GeneratePdf();
}
