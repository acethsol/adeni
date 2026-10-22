namespace Adeni.Infrastructure.Reporting;

/// <summary>
/// Option 3 wordmark shipped with the API (mirrors <c>@adeni/brand</c> <c>adeni-wordmark.png</c>).
/// </summary>
internal static class QuestPdfAdeniBrandAssets
{
    private static byte[]? _wordmarkBytes;

    public static byte[] GetPoweredByWordmarkBytes()
    {
        if (_wordmarkBytes is not null)
        {
            return _wordmarkBytes;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Brand", "adeni-wordmark.png");
        _wordmarkBytes = File.ReadAllBytes(path);
        return _wordmarkBytes;
    }
}
