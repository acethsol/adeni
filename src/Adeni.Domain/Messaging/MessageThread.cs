namespace Adeni.Domain.Messaging;

using Adeni.Domain.Tenancy;

public enum ThreadStatus
{
    Open = 0,
    Closed = 1,
}

public sealed class MessageThread : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid? BookingId { get; set; }

    public ThreadStatus Status { get; set; }

    public string? Subject { get; set; }

    public DateTimeOffset LastMessageAt { get; set; }

    public int BusinessUnreadCount { get; set; }

    public int CustomerUnreadCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public enum MessageSenderType
{
    Customer = 0,
    Business = 1,
}

public sealed class Message : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid ThreadId { get; set; }

    public Guid TenantId { get; set; }

    public MessageSenderType SenderType { get; set; }

    public string SenderAuth0Sub { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public MessageThread? Thread { get; set; }
}
