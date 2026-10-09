namespace Adeni.Application.Storage;

using System.Text.Json;

/// <summary>
/// Shared JSON codec for tenant gallery image keys (stored on <c>BusinessProfile.GalleryImageKeysJson</c>).
/// Lives in Application so Infrastructure modules do not cross-reference Storage.
/// </summary>
public static class GalleryImageKeys
{
    public const int MaxCount = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static List<string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            return parsed?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.Ordinal)
                .Take(MaxCount)
                .ToList()
                ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string? Serialize(IReadOnlyList<string> keys)
    {
        var cleaned = keys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(MaxCount)
            .ToList();

        return cleaned.Count == 0 ? null : JsonSerializer.Serialize(cleaned, JsonOptions);
    }
}
