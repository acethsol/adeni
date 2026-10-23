namespace Adeni.Infrastructure.Admin;

using Adeni.Application.Admin;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class AdminAuditLogQueryService(AdeniDbContext dbContext) : IAdminAuditLogQueryService
{
    public async Task<Result<AdminPagedResult<AdminAuditLogListItem>>> ListAsync(
        string? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var paging = AdminPaging.Normalize(page, pageSize);
        if (paging.IsFailure)
        {
            return Result.Failure<AdminPagedResult<AdminAuditLogListItem>>(paging.Error);
        }

        var (effectivePage, effectivePageSize) = paging.Value;
        var query = dbContext.AuditLogs.AsNoTracking();

        var trimmedAction = action?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedAction))
        {
            query = query.Where(x => x.Action == trimmedAction);
        }

        if (from.HasValue)
        {
            query = query.Where(x => x.OccurredAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.OccurredAt <= to.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = (effectivePage - 1) * effectivePageSize;

        var rows = await query
            .OrderByDescending(x => x.OccurredAt)
            .Skip(skip)
            .Take(effectivePageSize)
            .Select(x => new AdminAuditLogListItem(
                x.Id,
                x.ActorId,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.CorrelationId,
                x.OccurredAt,
                x.MetadataJson))
            .ToListAsync(cancellationToken);

        return Result.Success(new AdminPagedResult<AdminAuditLogListItem>(
            rows,
            effectivePage,
            effectivePageSize,
            totalCount));
    }
}
