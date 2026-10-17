namespace Adeni.Domain.Messaging;

using Adeni.Domain.Tenancy;

public enum ConversationStatus
{
    Open = 0,
    Closed = 1,
}

public enum ConversationContextType
{
    None = 0,
    Booking = 1,
    Quote = 2,
    Profile = 3,
}

public enum ConversationParticipantType
{
    Customer = 0,
    BusinessUser = 1,
    Bot = 2,
}

public enum ConversationMessageType
{
    Text = 0,
    System = 1,
    AttachmentOnly = 2,
}

public enum MessageModerationStatus
{
    None = 0,
    Hidden = 1,
    Flagged = 2,
}

public static class MessagingBotKeys
{
    public const string Faq = "faq";
}

/// <summary>Tenant-scoped conversation between a customer and a business.</summary>
public sealed class Conversation : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CustomerId { get; set; }

    public ConversationContextType ContextType { get; set; }

    public Guid? ContextId { get; set; }

    public ConversationStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastMessageAt { get; set; }

    public Guid? CreatedByParticipantId { get; set; }

    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();

    public ICollection<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();
}

public sealed class ConversationParticipant : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Guid TenantId { get; set; }

    public ConversationParticipantType ParticipantType { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid? BusinessUserId { get; set; }

    public string? BotKey { get; set; }

    public string? Auth0Sub { get; set; }

    public Guid? LastReadMessageId { get; set; }

    public DateTimeOffset? LastReadAt { get; set; }

    public bool IsMuted { get; set; }

    public DateTimeOffset JoinedAt { get; set; }

    public Conversation? Conversation { get; set; }
}

public sealed class ConversationMessage : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Guid TenantId { get; set; }

    public Guid SenderParticipantId { get; set; }

    public ConversationMessageType Type { get; set; }

    public string? Body { get; set; }

    public string? ClientMessageId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public MessageModerationStatus ModerationStatus { get; set; }

    public Conversation? Conversation { get; set; }

    public ConversationParticipant? SenderParticipant { get; set; }

    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
}

public sealed class MessageAttachment
{
    public Guid Id { get; set; }

    public Guid MessageId { get; set; }

    public string BlobKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ConversationMessage? Message { get; set; }
}
