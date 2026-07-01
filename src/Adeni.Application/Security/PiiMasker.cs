namespace Adeni.Application.Security;

public static class PiiMasker
{
    private const string Redacted = "[REDACTED]";

    public static string MaskEmail(string? email) =>
        string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Contains('@', StringComparison.Ordinal)
                ? MaskLocalPart(email)
                : Redacted;

    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length <= 4
            ? Redacted
            : $"{Redacted}{digits[^4..]}";
    }

    public static string MaskMessageBody(string? body) =>
        string.IsNullOrWhiteSpace(body) ? string.Empty : Redacted;

    public static string SanitizeLogValue(string? value, PiiKind kind) =>
        kind switch
        {
            PiiKind.Email => MaskEmail(value),
            PiiKind.Phone => MaskPhone(value),
            PiiKind.MessageBody => MaskMessageBody(value),
            PiiKind.None => value ?? string.Empty,
            _ => Redacted
        };

    private static string MaskLocalPart(string email)
    {
        var atIndex = email.IndexOf('@');
        var local = email[..atIndex];
        var domain = email[atIndex..];
        var visible = local.Length <= 1 ? "*" : $"{local[0]}***";
        return $"{visible}{domain}";
    }
}

public enum PiiKind
{
    None,
    Email,
    Phone,
    MessageBody
}
