namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

public enum QueueTicketStatus
{
    Waiting = 0,
    Called = 1,
    InService = 2,
    Done = 3,
    NoShow = 4,
    Cancelled = 5,
}

public sealed class QueueTicket : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid? ServiceOfferingId { get; set; }

    public Guid? StaffMemberId { get; set; }

    public Guid? CustomerId { get; set; }

    public string GuestName { get; set; } = string.Empty;

    public string GuestPhone { get; set; } = string.Empty;

    public QueueTicketStatus Status { get; set; } = QueueTicketStatus.Waiting;

    public int PartySize { get; set; } = 1;

    public string? Notes { get; set; }

    public string PublicToken { get; set; } = string.Empty;

    public Guid? BookingId { get; set; }

    public DateTimeOffset JoinedAt { get; set; }

    public DateTimeOffset? CalledAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? NoShowAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ServiceOffering? ServiceOffering { get; set; }

    public StaffMember? StaffMember { get; set; }
}
