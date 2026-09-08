namespace Adeni.Domain.Tenancy;

/// <summary>Brand-level profile (1:1 with tenant). Branches live in <see cref="BusinessLocation"/>.</summary>
public sealed class BusinessProfile : ITenantEntity
{
    public Guid TenantId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string CategorySlug { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? CoverImageKey { get; set; }

    public BusinessType BusinessType { get; set; } = BusinessType.ScheduledAppointment;

    public bool AutoConfirmBookings { get; set; }

    /// <summary>Deposit percentage (0–100) charged at booking confirm when deposits capability is enabled.</summary>
    public int DepositPercent { get; set; }

    /// <summary>When enabled, rule-based FAQ replies are sent automatically on incoming customer messages.</summary>
    public bool FaqAutoResponderEnabled { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant? Tenant { get; set; }

    public ICollection<BusinessLocation> Locations { get; set; } = new List<BusinessLocation>();
}
