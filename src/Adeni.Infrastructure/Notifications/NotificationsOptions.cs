namespace Adeni.Infrastructure.Notifications;

public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    public string DefaultCountryCode { get; set; } = "NG";

    public ResendNotificationOptions Resend { get; set; } = new();

    public TwilioNotificationOptions Twilio { get; set; } = new();

    public List<NotificationSenderMarketOptions> Senders { get; set; } = [];
}

public sealed class ResendNotificationOptions
{
    public string? ApiKey { get; set; }

    public string? From { get; set; }
}

public sealed class TwilioNotificationOptions
{
    public string? AccountSid { get; set; }

    public string? AuthToken { get; set; }

    public string? From { get; set; }
}

public sealed class NotificationSenderMarketOptions
{
    public string CountryCode { get; set; } = string.Empty;

    public string? FromEmail { get; set; }

    public string? SmsFrom { get; set; }
}
