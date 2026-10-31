namespace Adeni.Domain.Tenancy;

public sealed class LocationSecurityPolicy
{
    public Guid TenantId { get; set; }

    public bool RequirePresenceForSensitiveCustomerData { get; set; }

    public int PresenceValidityMinutes { get; set; } = 90;

    public bool AllowManagerPresenceOverride { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid? UpdatedByBusinessUserId { get; set; }
}
