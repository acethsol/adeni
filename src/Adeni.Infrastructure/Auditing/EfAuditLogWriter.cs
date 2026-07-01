namespace Adeni.Infrastructure.Auditing;

using Adeni.Application.Abstractions;
using Adeni.Domain.Auditing;
using Adeni.Infrastructure.Persistence;

public sealed class EfAuditLogWriter(AdeniDbContext dbContext) : IAuditLogWriter
{
    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(new AuditLogRecord
        {
            Id = entry.Id,
            ActorId = entry.ActorId,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            CorrelationId = entry.CorrelationId,
            OccurredAt = entry.OccurredAt,
            MetadataJson = entry.MetadataJson
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
