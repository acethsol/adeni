namespace Adeni.Infrastructure.Admin;

using Adeni.Application.Abstractions;
using Adeni.Application.Admin;
using Adeni.Application.Caching;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class AdminBusinessService(
    AdeniDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    ICorrelationContext correlationContext,
    ICacheService cache) : IAdminBusinessService
{
    public async Task<IReadOnlyList<PendingBusinessResponse>> GetPendingVerificationsAsync(
        CancellationToken cancellationToken = default)
    {
        var tenants = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Status == TenantStatus.PendingVerification)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Status,
                t.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        if (tenants.Count == 0)
        {
            return [];
        }

        var tenantIds = tenants.Select(x => x.Id).ToList();
        var locations = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => tenantIds.Contains(x.TenantId) && x.IsActive)
            .ToListAsync(cancellationToken);

        var documents = await dbContext.VerificationDocuments
            .AsNoTracking()
            .Where(x => tenantIds.Contains(x.TenantId))
            .ToListAsync(cancellationToken);

        return tenants.Select(tenant =>
        {
            var primary = locations
                .Where(x => x.TenantId == tenant.Id)
                .OrderByDescending(x => x.IsPrimary)
                .FirstOrDefault();

            var docs = documents
                .Where(x => x.TenantId == tenant.Id)
                .Select(x => new PendingVerificationDocumentResponse(
                    x.DocumentType.ToString().ToLowerInvariant(),
                    x.ReferenceNumber,
                    x.SubmittedAt))
                .ToList();

            return new PendingBusinessResponse(
                tenant.Id,
                tenant.Name,
                primary?.Slug ?? string.Empty,
                primary?.MarketId ?? string.Empty,
                tenant.Status,
                tenant.CreatedAt,
                docs);
        }).ToList();
    }

    public async Task<IReadOnlyList<AdminBusinessSummaryResponse>> ListBusinessesAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.Tenants
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Take(200)
            .Select(t => new AdminBusinessSummaryResponse(
                t.Id,
                t.Name,
                dbContext.BusinessLocations
                    .Where(location => location.TenantId == t.Id && location.IsActive && location.IsPrimary)
                    .Select(location => location.Slug)
                    .FirstOrDefault()
                    ?? dbContext.BusinessLocations
                        .Where(location => location.TenantId == t.Id && location.IsActive)
                        .Select(location => location.Slug)
                        .FirstOrDefault()
                    ?? string.Empty,
                t.Status,
                SubscriptionTierMapping.ToApiValue(t.SubscriptionTier),
                t.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<Result<Unit>> SetSubscriptionTierAsync(
        Guid tenantId,
        string tier,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Business"));
        }

        var normalizedTier = SubscriptionTierMapping.FromApiValue(tier);
        tenant.SubscriptionTier = normalizedTier;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogWriter.WriteAsync(new AuditEntry(
            Guid.NewGuid(),
            adminId,
            "tenant.subscription_tier_updated",
            "tenant",
            tenantId.ToString(),
            correlationContext.CorrelationId,
            DateTimeOffset.UtcNow,
            $"{{\"tier\":\"{SubscriptionTierMapping.ToApiValue(normalizedTier)}\"}}"),
            cancellationToken);

        return Result.Success(Unit.Value);
    }

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

        var slugs = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => x.Slug)
            .ToListAsync(cancellationToken);

        foreach (var slug in slugs)
        {
            await cache.RemoveAsync(CacheKeys.LocationProfile(slug), cancellationToken);
        }

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
