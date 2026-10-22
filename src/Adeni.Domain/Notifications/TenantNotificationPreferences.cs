namespace Adeni.Domain.Notifications;

using Adeni.Domain.Tenancy;

/// <summary>Per-tenant outbound notification channel toggles (booking reminders, waitlist, etc.).</summary>
public sealed class TenantNotificationPreferences : ITenantEntity
{
    public Guid TenantId { get; set; }

    public bool EmailEnabled { get; set; } = true;

    public bool PushEnabled { get; set; }

    public bool SmsRemindersEnabled { get; set; }

    /// <summary>BCP-47-style short code (en, fr) for default outbound copy when recipient locale unknown.</summary>
    public string DefaultNotificationLocale { get; set; } = "en";

    /// <summary>single = one locale per message; bilingual = one email with primary + secondary locale sections (SMS uses primary only).</summary>
    public string NotificationLanguageMode { get; set; } = "single";

    /// <summary>Second locale for bilingual email sections; null when single mode.</summary>
    public string? SecondaryNotificationLocale { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
