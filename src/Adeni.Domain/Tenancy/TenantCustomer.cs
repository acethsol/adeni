namespace Adeni.Domain.Tenancy;

public sealed class TenantCustomer : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Linked platform consumer (identity.customers) when known.</summary>
    public Guid? CustomerId { get; set; }

    public string? NormalizedPhone { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset? LastBookingAt { get; set; }

    public DateTimeOffset? LastQueueJoinAt { get; set; }
}
