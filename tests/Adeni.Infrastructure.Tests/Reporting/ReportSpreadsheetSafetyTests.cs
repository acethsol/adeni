namespace Adeni.Infrastructure.Tests.Reporting;

using System.Text;
using Adeni.Application.Reporting;
using Adeni.Infrastructure.Reporting;
using ClosedXML.Excel;

public sealed class ReportSpreadsheetSafetyTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 6, 15, 12, 0, 0, TimeSpan.FromHours(1));

    [Fact]
    public void BuildBookingsRegisterCsv_EscapesTextAndPreservesNegativeAmount()
    {
        var bytes = ReportSpreadsheetBuilder.BuildBookingsRegisterCsv([
            new(Guid.NewGuid(), Timestamp, Timestamp, "Confirmed", "Cut, colour", "Àda\r\n\"Guest\"", "=1+1", -12.50m, "NGN"),
        ]);
        var csv = Encoding.UTF8.GetString(bytes);
        Assert.StartsWith("BookingId,StartAt,EndAt,Status,Service,Customer,Staff,Total,Currency" + Environment.NewLine, csv);
        Assert.Contains(",\"Cut, colour\",\"Àda\r\n\"\"Guest\"\"\",'=1+1,-12.50,NGN", csv);
        Assert.Contains(Timestamp.ToString("O"), csv);
    }

    [Fact]
    public void BuildCustomerAndAdminCsv_ProtectsUntrustedText()
    {
        var customers = ReportSpreadsheetBuilder.BuildTenantCustomersCsv([
            new(Guid.NewGuid(), "@SUM(A1)", "+234***", "a,b@example.com", "guest", Timestamp, Timestamp),
        ]);
        Assert.Contains(",'@SUM(A1),'+234***,\"a,b@example.com\",guest,", customers);
        var transactions = ReportSpreadsheetBuilder.BuildAdminTransactionsCsv([
            new("payment", Guid.NewGuid(), Guid.NewGuid(), "=1+1", "+ref", "-event", "@sku", -1250L, "NGN", "paid", Timestamp),
        ]);
        Assert.Contains(",'=1+1,'+ref,'-event,'@sku,-1250,NGN,paid,", transactions);
    }

    [Fact]
    public void BuildSalesSummaryCsv_PreservesNegativeMetricAndProtectsText()
    {
        var csv = Encoding.UTF8.GetString(ReportSpreadsheetBuilder.BuildSalesSummaryCsv([
            new("collected_amount", "-12.50", "NGN"), new("=metric", "-1+2", "@currency"),
        ]));
        Assert.Contains("collected_amount,-12.50,NGN", csv);
        Assert.Contains("'=metric,'-1+2,'@currency", csv);
    }

    [Fact]
    public void RemainingCsvReports_UseSharedTextProtection()
    {
        Assert.Contains("'=name", ReportSpreadsheetBuilder.BuildStaffRosterCsv([
            new("=name", "role", true, "none", 1, -1),
        ]));
        Assert.Contains("'=name", ReportSpreadsheetBuilder.BuildTenantLocationsCsv([
            new("slug", "=name", "lagos", "address", "area", true, true),
        ]));
        Assert.Contains("'=name", ReportSpreadsheetBuilder.BuildTenantServicesCsv([
            new("=name", null, -12.50m, "NGN", 30, null, true, false, 1),
        ]));
        Assert.Contains("'=name", ReportSpreadsheetBuilder.BuildAdminBusinessDirectoryCsv([
            new(Guid.NewGuid(), "=name", "slug", "verified", "pro", "active", null, Timestamp),
        ]));
        Assert.Contains("'=name", ReportSpreadsheetBuilder.BuildAdminPendingVerificationsCsv([
            new(Guid.NewGuid(), "=name", "slug", "lagos", Timestamp, 1),
        ]));
        Assert.Contains("'=name", ReportSpreadsheetBuilder.BuildAdminPricingCatalogEntriesCsv([
            new("=name", "NG", "pro", "NGN", 100m, 2.5m, true),
        ]));
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+SUM(A1)")]
    [InlineData("-1+2")]
    [InlineData("@SUM(A1)")]
    public void BuildXlsx_FormulaLookingText_IsTextAndNeverAFormula(string text)
    {
        var id = Guid.NewGuid();
        AssertText(ReportSpreadsheetBuilder.BuildBookingsRegisterXlsx([
            new(id, Timestamp, Timestamp, text, text, text, text, -12.50m, text),
        ]), "E2", text);
        AssertText(ReportSpreadsheetBuilder.BuildCompensationXlsx([
            new(Timestamp, id, text, text, -12.50m, text, null, null),
        ]), "C2", text);
        AssertText(ReportSpreadsheetBuilder.BuildTenantCustomersXlsx([
            new(id, text, text, text, text, Timestamp, Timestamp),
        ]), "B2", text);
        AssertText(ReportSpreadsheetBuilder.BuildTenantServicesXlsx([
            new(text, text, -12.50m, text, 30, text, true, false, 1),
        ]), "A2", text);
        AssertText(ReportSpreadsheetBuilder.BuildAdminBusinessDirectoryXlsx([
            new(id, text, text, text, text, text, Timestamp, Timestamp),
        ]), "B2", text);
        AssertText(ReportSpreadsheetBuilder.BuildAdminPendingVerificationsXlsx([
            new(id, text, text, text, Timestamp, 1),
        ]), "B2", text);
        AssertText(ReportSpreadsheetBuilder.BuildAdminTransactionsXlsx([
            new(text, id, id, text, text, text, text, -1250, text, text, Timestamp),
        ]), "D2", text);
        AssertText(ReportSpreadsheetBuilder.BuildAdminPricingCatalogEntriesXlsx([
            new(text, text, text, text, -12.50m, 2.5m, true),
        ]), "A2", text);
    }

    [Fact]
    public void BuildXlsx_RetainsNumbersBooleansHeadersAndOffsetTimestamps()
    {
        using var bookings = Open(ReportSpreadsheetBuilder.BuildBookingsRegisterXlsx([
            new(Guid.NewGuid(), Timestamp, Timestamp, "Confirmed", "Cut", null, null, -12.50m, "NGN"),
        ]));
        var sheet = bookings.Worksheet(1);
        Assert.Equal("BookingId", sheet.Cell("A1").GetString());
        Assert.Equal("Currency", sheet.Cell("I1").GetString());
        Assert.Equal(XLDataType.Number, sheet.Cell("H2").DataType);
        Assert.Equal(-12.50, sheet.Cell("H2").GetDouble());
        // ISO text intentionally preserves DateTimeOffset offsets.
        Assert.Equal(XLDataType.Text, sheet.Cell("B2").DataType);
        Assert.Equal(Timestamp.ToString("O"), sheet.Cell("B2").GetString());
        using var catalog = Open(ReportSpreadsheetBuilder.BuildAdminPricingCatalogEntriesXlsx([
            new("v1", "NG", "pro", "NGN", 100m, 2.5m, true),
        ]));
        Assert.Equal(XLDataType.Number, catalog.Worksheet(1).Cell("E2").DataType);
        Assert.Equal(XLDataType.Number, catalog.Worksheet(1).Cell("F2").DataType);
        Assert.Equal(2.5, catalog.Worksheet(1).Cell("F2").GetDouble());
        Assert.Equal(XLDataType.Boolean, catalog.Worksheet(1).Cell("G2").DataType);
    }

    private static XLWorkbook Open(byte[] bytes) => new(new MemoryStream(bytes));

    private static void AssertText(byte[] bytes, string address, string expected)
    {
        using var workbook = Open(bytes);
        var cell = workbook.Worksheet(1).Cell(address);
        Assert.False(cell.HasFormula);
        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.Equal(expected, cell.GetString());
    }
}
