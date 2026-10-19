namespace Adeni.Infrastructure.Tests.Notifications;

using Adeni.Application.Notifications;
using Adeni.Domain.Messaging;
using Adeni.Domain.Messaging.Events;
using Adeni.Infrastructure.Notifications;

public sealed class MessagingNotificationHandlerTests
{
    [Fact]
    public async Task Customer_message_notifies_tenant_inbox()
    {
        var dispatcher = new CapturingDispatcher();
        var handler = new MessagingNotificationHandler(dispatcher);
        var tenantId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        await handler.HandleAsync(
            new ConversationMessageCreated(
                Guid.NewGuid(),
                conversationId,
                tenantId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                ConversationParticipantType.Customer,
                "Hello there",
                HasAttachments: false,
                DateTimeOffset.UtcNow));

        Assert.Single(dispatcher.Messages);
        Assert.Equal("email", dispatcher.Messages[0].Channel);
        Assert.Equal($"tenant:{tenantId:D}", dispatcher.Messages[0].RecipientKey);
        Assert.Equal(tenantId, dispatcher.Messages[0].TenantId);
        Assert.Contains("Hello there", dispatcher.Messages[0].Body);
    }

    [Fact]
    public async Task Business_message_notifies_customer()
    {
        var dispatcher = new CapturingDispatcher();
        var handler = new MessagingNotificationHandler(dispatcher);
        var customerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await handler.HandleAsync(
            new ConversationMessageCreated(
                Guid.NewGuid(),
                Guid.NewGuid(),
                tenantId,
                customerId,
                Guid.NewGuid(),
                ConversationParticipantType.BusinessUser,
                null,
                HasAttachments: true,
                DateTimeOffset.UtcNow));

        Assert.Single(dispatcher.Messages);
        Assert.Equal(customerId.ToString("D"), dispatcher.Messages[0].RecipientKey);
        Assert.Contains("Attachment", dispatcher.Messages[0].Body);
    }

    private sealed class CapturingDispatcher : INotificationDispatcher
    {
        public List<NotificationMessage> Messages { get; } = [];

        public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
