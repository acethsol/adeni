namespace Adeni.Application.Messaging;

using Adeni.Application.Admin;
using Adeni.Application.Common;
using Adeni.Domain.Common;
using Adeni.Domain.Messaging;
using Adeni.Domain.Subscriptions;

public sealed record CreateConversationRequest(
    Guid TenantId,
    ConversationContextType ContextType = ConversationContextType.None,
    Guid? ContextId = null);

public sealed record SendConversationMessageRequest(
    string Body,
    string? ClientMessageId = null);

public sealed record ConversationSummaryResponse(
    Guid Id,
    Guid TenantId,
    ConversationContextType ContextType,
    Guid? ContextId,
    string CustomerDisplayName,
    string? BusinessName,
    string? Preview,
    string? LastBusinessReplierName,
    DateTimeOffset LastMessageAt,
    bool NeedsAttention);

public sealed record ConversationMessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid SenderParticipantId,
    string SenderType,
    string? SenderDisplayName,
    string? Body,
    string? ClientMessageId,
    DateTimeOffset CreatedAt);

public sealed record ConversationDetailResponse(
    Guid Id,
    Guid TenantId,
    ConversationContextType ContextType,
    Guid? ContextId,
    string Status,
    string CustomerDisplayName,
    string? BusinessName,
    IReadOnlyList<ConversationMessageResponse> Messages,
    Guid? NextBeforeId);

public sealed record UnreadCountResponse(int Count);

public sealed record WhatsAppLinkResponse(string Url, string? PhoneMasked);

public sealed record MessageTemplateResponse(string Key, string Label, string Body);

public sealed record MessagingSettingsResponse(bool FaqAutoResponderEnabled);

public sealed record UpdateMessagingSettingsRequest(bool FaqAutoResponderEnabled);

public enum MessagingActorRole
{
    Customer,
    Business,
}

public interface IConversationService
{
    Task<Result<ConversationSummaryResponse>> CreateOrGetAsync(
        string customerAuth0Sub,
        CreateConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ConversationMessageResponse>> SendAsync(
        string auth0Sub,
        MessagingActorRole role,
        Guid conversationId,
        SendConversationMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationSummaryResponse>> ListForCustomerAsync(
        string customerAuth0Sub,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ConversationSummaryResponse>>> ListForTenantAsync(
        Guid tenantId,
        SubscriptionTier tier,
        int page = 1,
        int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default);

    Task<Result<ConversationDetailResponse>> GetAsync(
        string auth0Sub,
        MessagingActorRole role,
        Guid conversationId,
        Guid? beforeId = null,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<Result<UnreadCountResponse>> GetTeamUnreadCountForTenantAsync(
        Guid tenantId,
        SubscriptionTier tier,
        CancellationToken cancellationToken = default);

    Task<Result> MarkReadAsync(
        string auth0Sub,
        MessagingActorRole role,
        Guid conversationId,
        Guid? messageId = null,
        CancellationToken cancellationToken = default);

    Task<Result<WhatsAppLinkResponse>> BuildBusinessWhatsAppLinkAsync(
        string slug,
        CancellationToken cancellationToken = default);

    Task<Result<WhatsAppLinkResponse>> BuildBookingWhatsAppLinkAsync(
        string customerAuth0Sub,
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<MessageTemplateResponse>>> GetTemplatesForTenantAsync(
        Guid tenantId,
        SubscriptionTier tier,
        CancellationToken cancellationToken = default);

    Task<Result<MessagingSettingsResponse>> GetMessagingSettingsAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken = default);

    Task<Result<MessagingSettingsResponse>> UpdateMessagingSettingsAsync(
        Guid tenantId,
        UpdateMessagingSettingsRequest request,
        string auth0Sub,
        CancellationToken cancellationToken = default);
}

/// <summary>Realtime fan-out after durable commit. No-op implementations are valid for tests.</summary>
public interface IMessagingRealtimePublisher
{
    Task PublishMessageCreatedAsync(
        Guid conversationId,
        Guid tenantId,
        ConversationMessageResponse message,
        CancellationToken cancellationToken = default);

    Task PublishReadUpdatedAsync(
        Guid conversationId,
        Guid tenantId,
        Guid participantId,
        Guid lastReadMessageId,
        CancellationToken cancellationToken = default);

    Task PublishConversationUpdatedAsync(
        Guid tenantId,
        ConversationSummaryResponse summary,
        CancellationToken cancellationToken = default);
}
