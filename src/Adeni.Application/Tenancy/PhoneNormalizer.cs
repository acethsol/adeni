namespace Adeni.Application.Tenancy;

using System.Text.RegularExpressions;

public static class PhoneNormalizer
{
    private static readonly Regex PhoneDigits = new(@"[^\d+]", RegexOptions.Compiled);

    public static string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var trimmed = phone.Trim();
        var digits = PhoneDigits.Replace(trimmed, string.Empty);
        if (digits.Length == 0)
        {
            return null;
        }

        if (!digits.StartsWith('+'))
        {
            digits = digits.StartsWith("00", StringComparison.Ordinal)
                ? "+" + digits[2..]
                : "+" + digits;
        }

        return digits.Length > 40 ? digits[..40] : digits;
    }
}
