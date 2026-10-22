namespace Adeni.Infrastructure.Reporting;

using Adeni.Application.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

internal static class BillingDocumentQuestPdfTemplate
{
    internal static byte[] Render(BillingDocumentReportData data)
    {
        QuestPdfLicenseInitializer.EnsureReady();
        var kindLabel = data.Kind == ReportKind.BillingReceipt ? "Receipt" : "Invoice";
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Element(header =>
                    QuestPdfBrandingLayout.ComposeBusinessHeader(
                        header,
                        data.BusinessNameSnapshot,
                        data.BusinessLogoBytes,
                        column =>
                        {
                            column.Item().Text(kindLabel).FontSize(14).FontColor(QuestPdfBrandColors.Purple);
                            column.Item().Text(data.DocumentNumber).FontSize(12);
                        }));

                page.Content().PaddingVertical(20).Column(col =>
                {
                    if (!string.IsNullOrWhiteSpace(data.CustomerNameSnapshot))
                    {
                        col.Item().Text($"Customer: {data.CustomerNameSnapshot}");
                    }

                    if (data.IssuedAt is DateTimeOffset issued)
                    {
                        col.Item().Text($"Date: {issued:yyyy-MM-dd HH:mm} UTC");
                    }

                    if (!string.IsNullOrWhiteSpace(data.MethodLabel))
                    {
                        col.Item().Text($"Method: {data.MethodLabel}");
                    }

                    col.Item().PaddingTop(16).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Description").SemiBold();
                            header.Cell().AlignRight().Text("Qty").SemiBold();
                            header.Cell().AlignRight().Text("Unit").SemiBold();
                            header.Cell().AlignRight().Text("Total").SemiBold();
                        });

                        foreach (var line in data.Lines.OrderBy(x => x.SortOrder))
                        {
                            table.Cell().Text(line.Description);
                            table.Cell().AlignRight().Text(line.Quantity.ToString("0.##"));
                            table.Cell().AlignRight().Text($"{line.UnitAmount:0.00} {data.Currency}");
                            table.Cell().AlignRight().Text($"{line.LineTotal:0.00} {data.Currency}");
                        }
                    });

                    col.Item().AlignRight().PaddingTop(12)
                        .Text($"Total: {data.Total:0.00} {data.Currency}")
                        .FontSize(14)
                        .SemiBold();
                });

                page.Footer().Element(QuestPdfBrandingLayout.ComposePoweredByFooter);
            });
        }).GeneratePdf();
    }
}
