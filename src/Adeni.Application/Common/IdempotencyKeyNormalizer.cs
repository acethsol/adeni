namespace Adeni.Application.Common;

public static class IdempotencyKeyNormalizer
{
    public static string? Normalize(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        var trimmed = idempotencyKey.Trim();
        return trimmed.Length is > 0 and <= 128 ? trimmed : null;
    }
}
