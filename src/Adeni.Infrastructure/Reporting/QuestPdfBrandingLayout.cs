namespace Adeni.Infrastructure.Reporting;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

internal static class QuestPdfBrandingLayout
{
    internal static void ComposeBusinessHeader(
        IContainer container,
        string businessName,
        byte[]? businessLogoBytes,
        Action<ColumnDescriptor>? extra = null)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            if (businessLogoBytes is { Length: > 0 })
            {
                column.Item().Height(48).Image(businessLogoBytes).FitArea();
            }
            else
            {
                column.Item().Text(businessName).SemiBold().FontSize(16).FontColor(QuestPdfBrandColors.Charcoal);
            }

            extra?.Invoke(column);
        });
    }

    internal static void ComposePoweredByFooter(IContainer container)
    {
        container.AlignCenter().Row(row =>
        {
            row.AutoItem().AlignMiddle().Text("Powered by").FontSize(8).FontColor(QuestPdfBrandColors.Teal);
            row.ConstantItem(6);
            row.AutoItem().Height(12).Image(QuestPdfAdeniBrandAssets.GetPoweredByWordmarkBytes()).FitHeight();
        });
    }
}
