namespace Adeni.Domain.Notifications;

using Adeni.Domain.Tenancy;

public sealed class TenantNotificationTemplateOverride : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string TemplateId { get; set; } = string.Empty;

    public string Locale { get; set; } = "en";

    public string? SubjectOverride { get; set; }

    public string? BodyOverride { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
