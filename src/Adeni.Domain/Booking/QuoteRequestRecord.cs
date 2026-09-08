namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

public sealed class QuoteRequestRecord : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CustomerId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? ServiceAddress { get; set; }

    /// <summary>JSON array of media storage keys for job photos.</summary>
    public string? PhotoKeysJson { get; set; }

    public QuoteRequestStatus Status { get; set; } = QuoteRequestStatus.Submitted;

    public decimal? QuotedAmount { get; set; }

    public string? QuotedCurrency { get; set; }

    public string? QuoteNotes { get; set; }

    public Guid? ServiceOfferingId { get; set; }

    public DateTimeOffset? ProposedStartAt { get; set; }

    public DateTimeOffset? ProposedEndAt { get; set; }

    public DateTimeOffset? QuotedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public Guid? BookingId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
