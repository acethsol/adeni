namespace Adeni.Infrastructure.Messaging;

using Adeni.Application.Messaging;
using Adeni.Application.Tenancy;
using Adeni.Domain.Messaging;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerCommunicationActivityQuery(AdeniDbContext db) : ICustomerCommunicationActivityQuery
{
    public async Task<CustomerActivityBatch> GetActivityAsync(CustomerProfileScope scope, DateTimeOffset from, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        // Conversations have no branch of their own. Omit them in branch mode rather than infer scope from another module.
        if (scope.CustomerId is null || scope.LocationId is not null) return new([]);
        var rows = await db.ConversationMessages.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.Conversation != null
                && x.Conversation.TenantId == scope.TenantId && x.Conversation.CustomerId == scope.CustomerId
                && x.CreatedAt >= from && x.CreatedAt <= asOf && x.DeletedAt == null
                && x.ModerationStatus == MessageModerationStatus.None)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new { x.Id, x.ConversationId, x.CreatedAt }).Take(101).ToArrayAsync(cancellationToken);
        // SourceId is the conversation link; the stable activity id identifies the individual message.
        return new(rows.Take(100).Select(x => new CustomerActivityItem($"messaging.sent:{x.Id:N}", "messageSent",
            x.CreatedAt, "messaging", x.ConversationId)).ToArray(), rows.Length > 100);
    }
}
