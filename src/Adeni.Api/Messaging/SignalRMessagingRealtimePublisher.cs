namespace Adeni.Api.Messaging;

using Adeni.Application.Messaging;
using Microsoft.AspNetCore.SignalR;

public sealed class SignalRMessagingRealtimePublisher(IHubContext<MessagingHub> hub) : IMessagingRealtimePublisher
{
    public Task PublishMessageCreatedAsync(
        Guid conversationId,
        Guid tenantId,
        ConversationMessageResponse message,
        CancellationToken cancellationToken = default) =>
        hub.Clients
            .Groups(MessagingHub.ConversationGroup(conversationId), MessagingHub.TenantGroup(tenantId))
            .SendAsync("MessageCreated", message, cancellationToken);

    public Task PublishReadUpdatedAsync(
        Guid conversationId,
        Guid tenantId,
        Guid participantId,
        Guid lastReadMessageId,
        CancellationToken cancellationToken = default) =>
        hub.Clients
            .Group(MessagingHub.ConversationGroup(conversationId))
            .SendAsync(
                "ReadUpdated",
                new { conversationId, participantId, lastReadMessageId },
                cancellationToken);

    public Task PublishConversationUpdatedAsync(
        Guid tenantId,
        ConversationSummaryResponse summary,
        CancellationToken cancellationToken = default) =>
        hub.Clients
            .Group(MessagingHub.TenantGroup(tenantId))
            .SendAsync("ConversationUpdated", summary, cancellationToken);
}
