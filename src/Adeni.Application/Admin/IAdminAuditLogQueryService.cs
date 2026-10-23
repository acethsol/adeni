namespace Adeni.Application.Admin;

using Adeni.Domain.Common;

public sealed record AdminAuditLogListItem(
    Guid Id,
    string ActorId,
    string Action,
    string EntityType,
    string EntityId,
    string CorrelationId,
    DateTimeOffset OccurredAt,
    string? MetadataJson);

public interface IAdminAuditLogQueryService
{
    Task<Result<AdminPagedResult<AdminAuditLogListItem>>> ListAsync(
        string? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
