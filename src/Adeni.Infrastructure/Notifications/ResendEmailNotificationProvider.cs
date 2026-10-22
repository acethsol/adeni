namespace Adeni.Infrastructure.Notifications;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Adeni.Application.Notifications;
using Adeni.Application.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class ResendEmailNotificationProvider(
    HttpClient httpClient,
    IOptions<NotificationsOptions> options,
    ILogger<ResendEmailNotificationProvider> logger) : IEmailNotificationProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task SendAsync(EmailNotificationRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = options.Value.Resend.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Resend API key is not configured.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = JsonContent.Create(
            new
            {
                from = request.FromEmail,
                to = new[] { request.ToEmail },
                subject = request.Subject,
                html = request.Body,
            },
            options: JsonOptions);

        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Resend email failed for tenant {TenantId} to {Recipient} — HTTP {StatusCode}",
                request.TenantId,
                PiiMasker.MaskEmail(request.ToEmail),
                (int)response.StatusCode);
            response.EnsureSuccessStatusCode();
        }
    }
}
