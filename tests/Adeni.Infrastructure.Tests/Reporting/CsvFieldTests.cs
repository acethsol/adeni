namespace Adeni.Infrastructure.Tests.Reporting;

using Adeni.Infrastructure.Serialization;

public sealed class CsvFieldTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("ordinary text", "ordinary text")]
    [InlineData("Àda 中文 💇", "Àda 中文 💇")]
    [InlineData("A,B", "\"A,B\"")]
    [InlineData("A\"B", "\"A\"\"B\"")]
    [InlineData("A\nB", "\"A\nB\"")]
    [InlineData("A\rB", "\"A\rB\"")]
    [InlineData("A\r\nB", "\"A\r\nB\"")]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+SUM(A1)", "'+SUM(A1)")]
    [InlineData("-1+2", "'-1+2")]
    [InlineData("-12.50", "'-12.50")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("  =1+1", "'  =1+1")]
    [InlineData("\t=1+1", "'\t=1+1")]
    [InlineData("\r=1+1", "\"'\r=1+1\"")]
    [InlineData("\u0000=1+1", "'\u0000=1+1")]
    [InlineData("=SUM(1,2)", "\"'=SUM(1,2)\"")]
    public void Format_Text_EscapesAndProtects(string? value, string expected) =>
        Assert.Equal(expected, CsvField.Format(value));

    [Fact]
    public void Format_TypedNegativeNumber_RemainsNumeric()
    {
        Assert.Equal("-12.50", CsvField.Format(-12.50m));
        Assert.Equal("-42", CsvField.Format(-42));
        Assert.Equal("-1000", CsvField.Format(-1000L));
    }

    [Fact]
    public void Format_NonEnglishCulture_PreservesNumericRepresentations()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal("-12.50", CsvField.Format(-12.50m));
            Assert.Equal("\"-12,50\"", CsvField.FormattedNumber("-12,50"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("-12.50", "-12.50")]
    [InlineData("+12.50", "+12.50")]
    [InlineData("-1+2", "'-1+2")]
    [InlineData("=1+1", "'=1+1")]
    public void FormattedNumber_ValidatesCompleteMetricValue(string value, string expected) =>
        Assert.Equal(expected, CsvField.FormattedNumber(value));
}
