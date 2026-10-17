namespace Adeni.Domain.Messaging.Events;

using Adeni.Domain.Events;

/// <summary>
/// Raised after a conversation message is durably committed.
/// Drives prefs-aware notification stubs (email/push adapters later).
/// </summary>
public sealed record ConversationMessageCreated(
    Guid MessageId,
    Guid ConversationId,
    Guid TenantId,
    Guid CustomerId,
    Guid SenderParticipantId,
    ConversationParticipantType SenderType,
    string? BodyPreview,
    bool HasAttachments,
    DateTimeOffset At) : IDomainEvent;
