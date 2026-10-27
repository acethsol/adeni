namespace Adeni.Infrastructure.Serialization;

using System.Globalization;

/// <summary>CSV field serialization shared by current and legacy export renderers.</summary>
internal static class CsvField
{
    public static string Format(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            string s => ProtectText(s),
            IFormattable typed => typed.ToString(null, CultureInfo.InvariantCulture),
            _ => throw new ArgumentException("CSV fields must be text or typed scalar values.", nameof(value)),
        };
        return Escape(text);
    }

    // Sales summary metrics arrive already formatted by the source module, not as typed numbers.
    public static string FormattedNumber(string value)
    {
        const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        return decimal.TryParse(value, styles, CultureInfo.InvariantCulture, out _)
            || decimal.TryParse(value, styles, CultureInfo.CurrentCulture, out _)
            ? Escape(value)
            : Format(value);
    }

    private static string ProtectText(string text)
    {
        var start = 0;
        while (start < text.Length && (char.IsWhiteSpace(text[start]) || char.IsControl(text[start])))
        {
            start++;
        }

        var leading = text.AsSpan(start);
        // Spreadsheet imports can ignore leading whitespace/control characters before a formula.
        return leading.Length > 0 && leading[0] is '=' or '+' or '-' or '@'
            || text.Length > 0 && text[0] is '\t' or '\r' or '\n'
            ? "'" + text
            : text;
    }

    private static string Escape(string text) =>
        text.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : text;
}
