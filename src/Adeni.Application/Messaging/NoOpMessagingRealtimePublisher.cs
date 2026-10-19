namespace Adeni.Application.Messaging;

public sealed class NoOpMessagingRealtimePublisher : IMessagingRealtimePublisher
{
    public Task PublishMessageCreatedAsync(
        Guid conversationId,
        Guid tenantId,
        ConversationMessageResponse message,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task PublishReadUpdatedAsync(
        Guid conversationId,
        Guid tenantId,
        Guid participantId,
        Guid lastReadMessageId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task PublishConversationUpdatedAsync(
        Guid tenantId,
        ConversationSummaryResponse summary,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
