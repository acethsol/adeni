namespace Adeni.Domain.Notifications;

using Adeni.Domain.Tenancy;

/// <summary>Per-tenant outbound notification channel toggles (booking reminders, waitlist, etc.).</summary>
public sealed class TenantNotificationPreferences : ITenantEntity
{
    public Guid TenantId { get; set; }

    public bool EmailEnabled { get; set; } = true;

    public bool PushEnabled { get; set; }

    public bool SmsWhatsAppReminderEnabled { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
