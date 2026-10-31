namespace Adeni.Api.Common;

using System.Text;

/// <summary>Reads payment/subscription webhook bodies with a conservative size cap before signature verification.</summary>
public static class WebhookBodyReader
{
    public const int MaxBytes = 512 * 1024;

    public static async Task<(bool Success, string? Body)> TryReadAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ContentLength is > MaxBytes)
        {
            return (false, null);
        }

        request.EnableBuffering();
        await using var ms = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        long total = 0;
        while ((read = await request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxBytes)
            {
                return (false, null);
            }

            ms.Write(buffer, 0, read);
        }

        request.Body.Position = 0;
        return (true, Encoding.UTF8.GetString(ms.ToArray()));
    }
}
