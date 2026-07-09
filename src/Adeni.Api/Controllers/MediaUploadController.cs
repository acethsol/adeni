namespace Adeni.Api.Controllers;

using Adeni.Application.Storage;
using Adeni.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
public sealed class MediaUploadController(
    IFileStorage fileStorage,
    UploadSignatureValidator signatureValidator,
    IOptions<StorageOptions> storageOptions) : ControllerBase
{
    [HttpPut("media-upload")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        [FromQuery] string key,
        [FromQuery] long expires,
        [FromQuery] string sig,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(storageOptions.Value.Provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(key)
            || !signatureValidator.TryValidate(key, expires, sig))
        {
            return Unauthorized();
        }

        await fileStorage.SaveAsync(
            key,
            Request.Body,
            Request.ContentType ?? "application/octet-stream",
            cancellationToken);

        return NoContent();
    }
}
