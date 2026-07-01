namespace Adeni.Domain.Auditing;

public sealed record AuditEntry(
    Guid Id,
    string ActorId,
    string Action,
    string EntityType,
    string EntityId,
    string CorrelationId,
    DateTimeOffset OccurredAt,
    string? MetadataJson = null);
