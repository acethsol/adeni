namespace Adeni.Domain.Auditing;

public sealed class AuditLogRecord
{
    public Guid Id { get; set; }

    public string ActorId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    public string? MetadataJson { get; set; }
}
