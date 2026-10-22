namespace Adeni.Infrastructure.Tests.Notifications;

using Adeni.Application.Notifications;
using Adeni.Domain.Common;
using Adeni.Domain.Messaging;
using Adeni.Domain.Messaging.Events;
using Adeni.Infrastructure.Notifications;

public sealed class MessagingNotificationHandlerTests
{
    [Fact]
    public async Task Customer_message_enqueues_business_stub_without_resolved_inbox_email()
    {
        var dispatcher = new CapturingDispatcher();
        var handler = new MessagingNotificationHandler(
            dispatcher,
            new FixedRecipientResolver(Result.Success(new NotificationContact(null, null))),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MessagingNotificationHandler>.Instance);
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
        Assert.Equal(string.Empty, dispatcher.Messages[0].RecipientKey);
        Assert.Equal(tenantId, dispatcher.Messages[0].TenantId);
        Assert.Equal(NotificationTemplateIds.ConversationBusiness, dispatcher.Messages[0].TemplateId);
        Assert.Equal("Hello there", dispatcher.Messages[0].TemplateData!["preview"]);
    }

    [Fact]
    public async Task Business_message_notifies_customer_email_when_resolved()
    {
        var dispatcher = new CapturingDispatcher();
        var customerEmail = "guest@example.com";
        var handler = new MessagingNotificationHandler(
            dispatcher,
            new FixedRecipientResolver(Result.Success(new NotificationContact(customerEmail, null))),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MessagingNotificationHandler>.Instance);
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
        Assert.Equal(customerEmail, dispatcher.Messages[0].RecipientKey);
        Assert.Equal(NotificationTemplateIds.ConversationCustomer, dispatcher.Messages[0].TemplateId);
        Assert.Equal("Attachment", dispatcher.Messages[0].TemplateData!["preview"]);
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

    private sealed class FixedRecipientResolver(Result<NotificationContact> contact) : INotificationRecipientResolver
    {
        public Task<Result<NotificationContact>> ResolvePlatformCustomerAsync(
            Guid customerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(contact);

        public Task<Result<NotificationContact>> ResolveBookingCustomerAsync(
            Guid tenantId,
            Guid bookingId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(contact);
    }
}
