namespace Adeni.Infrastructure.Notifications;

using System.Net.Http.Headers;
using System.Text;
using Adeni.Application.Notifications;
using Adeni.Application.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class TwilioSmsNotificationProvider(
    HttpClient httpClient,
    IOptions<NotificationsOptions> options,
    ILogger<TwilioSmsNotificationProvider> logger) : ISmsNotificationProvider
{
    public async Task SendAsync(SmsNotificationRequest request, CancellationToken cancellationToken = default)
    {
        var twilio = options.Value.Twilio;
        if (string.IsNullOrWhiteSpace(twilio.AccountSid) || string.IsNullOrWhiteSpace(twilio.AuthToken))
        {
            throw new InvalidOperationException("Twilio credentials are not configured.");
        }

        var url = $"https://api.twilio.com/2010-04-01/Accounts/{twilio.AccountSid}/Messages.json";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{twilio.AccountSid}:{twilio.AuthToken}"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        httpRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = request.ToPhoneE164,
            ["From"] = request.FromPhoneOrSenderId,
            ["Body"] = request.Body,
        });

        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Twilio SMS failed for tenant {TenantId} to {Recipient} — HTTP {StatusCode}",
                request.TenantId,
                PiiMasker.MaskPhone(request.ToPhoneE164),
                (int)response.StatusCode);
            response.EnsureSuccessStatusCode();
        }
    }
}
