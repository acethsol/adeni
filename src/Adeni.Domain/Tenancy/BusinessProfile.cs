namespace Adeni.Domain.Tenancy;

/// <summary>Brand-level profile (1:1 with tenant). Branches live in <see cref="BusinessLocation"/>.</summary>
public sealed class BusinessProfile : ITenantEntity
{
    public Guid TenantId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string CategorySlug { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? CoverImageKey { get; set; }

    /// <summary>Square mark for public mini-site header (optional).</summary>
    public string? LogoImageKey { get; set; }

    /// <summary>Public page layout template id (<c>studio</c>, <c>spa</c>, <c>barber</c>, <c>luxe</c>).</summary>
    public string PublicPageTemplateId { get; set; } = PublicPageTemplates.Studio;

    /// <summary>Optional accent HEX (#RRGGBB) for public page CTAs.</summary>
    public string? PublicPageAccentColor { get; set; }

    public bool PublicPageShowAbout { get; set; } = true;

    public bool PublicPageShowServices { get; set; } = true;

    public bool PublicPageShowReviews { get; set; } = true;

    public bool PublicPageShowVisit { get; set; } = true;

    public BusinessType BusinessType { get; set; } = BusinessType.ScheduledAppointment;

    public bool AutoConfirmBookings { get; set; }

    /// <summary>Deposit percentage (0–100) charged at booking confirm when deposits capability is enabled.</summary>
    public int DepositPercent { get; set; }

    /// <summary>When enabled, rule-based FAQ replies are sent automatically on incoming customer messages.</summary>
    public bool FaqAutoResponderEnabled { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant? Tenant { get; set; }

    public ICollection<BusinessLocation> Locations { get; set; } = new List<BusinessLocation>();

    public ICollection<BusinessProfileCategory> Categories { get; set; } = new List<BusinessProfileCategory>();
}
