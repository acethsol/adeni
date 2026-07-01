namespace Adeni.Infrastructure.Admin;

using Adeni.Application.Abstractions;
using Adeni.Application.Admin;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class AdminBusinessService(
    AdeniDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    ICorrelationContext correlationContext) : IAdminBusinessService
{
    public async Task<IReadOnlyList<PendingBusinessResponse>> GetPendingVerificationsAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Status == TenantStatus.PendingVerification)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new PendingBusinessResponse(
                t.Id,
                t.Name,
                dbContext.BusinessProfiles
                    .Where(p => p.TenantId == t.Id)
                    .Select(p => p.Slug)
                    .FirstOrDefault() ?? string.Empty,
                t.Status,
                t.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<Result<Unit>> ApproveAsync(
        Guid tenantId,
        string adminId,
        CancellationToken cancellationToken = default) =>
        await UpdateStatusAsync(
            tenantId,
            adminId,
            TenantStatus.Verified,
            AuditActions.BusinessApproved,
            setVerifiedAt: true,
            cancellationToken);

    public async Task<Result<Unit>> RejectAsync(
        Guid tenantId,
        string adminId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
        {
            return Result.Failure<Unit>(Error.Validation("Rejection reason must be at least 10 characters."));
        }

        return await UpdateStatusAsync(
            tenantId,
            adminId,
            TenantStatus.Rejected,
            AuditActions.BusinessRejected,
            setVerifiedAt: false,
            cancellationToken,
            reason);
    }

    private async Task<Result<Unit>> UpdateStatusAsync(
        Guid tenantId,
        string adminId,
        TenantStatus status,
        string auditAction,
        bool setVerifiedAt,
        CancellationToken cancellationToken,
        string? metadata = null)
    {
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Business"));
        }

        tenant.Status = status;
        if (setVerifiedAt)
        {
            tenant.VerifiedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogWriter.WriteAsync(new AuditEntry(
            Guid.NewGuid(),
            adminId,
            auditAction,
            "tenant",
            tenantId.ToString(),
            correlationContext.CorrelationId,
            DateTimeOffset.UtcNow,
            metadata is null ? null : $"{{\"reason\":\"{metadata}\"}}"),
            cancellationToken);

        return Result.Success(Unit.Value);
    }
}
