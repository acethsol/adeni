namespace Adeni.Infrastructure.Admin;

using Adeni.Application.Abstractions;
using Adeni.Application.Admin;
using Adeni.Application.Caching;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class AdminBusinessService(
    AdeniDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    ICorrelationContext correlationContext,
    ICacheService cache) : IAdminBusinessService
{
    public async Task<Result<AdminPagedResult<PendingBusinessResponse>>> ListPendingVerificationsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var paging = AdminPaging.Normalize(page, pageSize);
        if (paging.IsFailure)
        {
            return Result.Failure<AdminPagedResult<PendingBusinessResponse>>(paging.Error);
        }

        var (effectivePage, effectivePageSize) = paging.Value;
        var needle = search?.Trim().ToLowerInvariant();

        var query = dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Status == TenantStatus.PendingVerification);

        if (!string.IsNullOrWhiteSpace(needle))
        {
            query = query.Where(t =>
                t.Name.ToLower().Contains(needle)
                || dbContext.BusinessLocations.Any(loc =>
                    loc.TenantId == t.Id
                    && loc.IsActive
                    && (loc.Slug.ToLower().Contains(needle) || loc.MarketId.ToLower().Contains(needle))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var tenants = await query
            .OrderBy(t => t.CreatedAt)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
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
            return Result.Success(new AdminPagedResult<PendingBusinessResponse>(
                [],
                effectivePage,
                effectivePageSize,
                totalCount));
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

        var items = tenants.Select(tenant =>
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

        return Result.Success(new AdminPagedResult<PendingBusinessResponse>(
            items,
            effectivePage,
            effectivePageSize,
            totalCount));
    }

    public async Task<Result<AdminPagedResult<AdminBusinessSummaryResponse>>> ListBusinessesAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var paging = AdminPaging.Normalize(page, pageSize);
        if (paging.IsFailure)
        {
            return Result.Failure<AdminPagedResult<AdminBusinessSummaryResponse>>(paging.Error);
        }

        var (effectivePage, effectivePageSize) = paging.Value;
        var needle = search?.Trim().ToLowerInvariant();

        var query = dbContext.Tenants.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(needle))
        {
            query = query.Where(t =>
                t.Name.ToLower().Contains(needle)
                || dbContext.BusinessLocations.Any(loc =>
                    loc.TenantId == t.Id
                    && loc.IsActive
                    && loc.Slug.ToLower().Contains(needle)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
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
                SubscriptionSkuMapping.ToApiValue(t.SubscriptionSku),
                TenantBillingStatusMapping.ToApiValue(t.BillingStatus),
                t.TrialEndsAt,
                t.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new AdminPagedResult<AdminBusinessSummaryResponse>(
            items,
            effectivePage,
            effectivePageSize,
            totalCount));
    }

    public async Task<AdminBusinessDirectoryStats> GetDirectoryStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var statusRows = await dbContext.Tenants
            .AsNoTracking()
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var tierRows = await dbContext.Tenants
            .AsNoTracking()
            .GroupBy(t => t.SubscriptionTier)
            .Select(g => new { Tier = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalCount = statusRows.Sum(x => x.Count);
        var verifiedCount = statusRows
            .Where(x => x.Status == TenantStatus.Verified)
            .Sum(x => x.Count);

        return new AdminBusinessDirectoryStats(
            totalCount,
            verifiedCount,
            statusRows
                .OrderBy(x => x.Status)
                .Select(x => new AdminStatusCount((int)x.Status, x.Count))
                .ToList(),
            tierRows
                .OrderBy(x => x.Tier)
                .Select(x => new AdminTierCount(SubscriptionTierMapping.ToApiValue(x.Tier), x.Count))
                .ToList());
    }

    public async Task<Result<AdminTenantCommercialTermsResponse>> GetTenantCommercialTermsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        return tenant is null
            ? Result.Failure<AdminTenantCommercialTermsResponse>(Error.NotFound("Business"))
            : Result.Success(AdminCommercialTermsMapper.ToResponse(tenant));
    }

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
        tenant.SubscriptionSku = SubscriptionSkuMapping.FromLegacyTier(normalizedTier);
        TenantBillingPolicy.EndTrial(tenant);
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

    public async Task<Result<Unit>> SetTenantCommercialTermsAsync(
        Guid tenantId,
        SetTenantCommercialTermsRequest request,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Business"));
        }

        var now = DateTimeOffset.UtcNow;

        if (request.EndTrial == true)
        {
            TenantBillingPolicy.EndTrial(tenant);
        }

        if (request.TrialDays.HasValue)
        {
            SubscriptionSku? trialSku = request.TrialSku is not null
                ? SubscriptionSkuMapping.FromApiValue(request.TrialSku)
                : tenant.TrialSku;
            TenantBillingPolicy.GrantTrial(tenant, request.TrialDays.Value, trialSku, now);
        }

        if (request.BillingStatus is not null)
        {
            tenant.BillingStatus = TenantBillingStatusMapping.FromApiValue(request.BillingStatus);
        }

        if (request.SubscriptionSku is not null)
        {
            tenant.SubscriptionSku = SubscriptionSkuMapping.FromApiValue(request.SubscriptionSku);
            TenantBillingPolicy.ApplySkuToLegacyTier(tenant);
        }

        if (request.TrialSku is not null && !request.TrialDays.HasValue)
        {
            tenant.TrialSku = SubscriptionSkuMapping.FromApiValue(request.TrialSku);
        }

        if (request.TrialEndsAt.HasValue)
        {
            tenant.TrialEndsAt = request.TrialEndsAt;
            if (request.TrialEndsAt.Value > now)
            {
                tenant.BillingStatus = TenantBillingStatus.Trialing;
            }
        }

        if (request.PaidThrough.HasValue)
        {
            tenant.PaidThrough = request.PaidThrough;
        }

        if (request.ClearDiscounts == true)
        {
            TenantBillingPolicy.ClearDiscounts(tenant);
        }

        if (request.DiscountPercent.HasValue)
        {
            if (request.DiscountPercent is < 0 or > 100)
            {
                return Result.Failure<Unit>(Error.Validation("Discount percent must be between 0 and 100."));
            }

            tenant.DiscountPercent = request.DiscountPercent;
        }

        if (request.DiscountFixedAmount.HasValue)
        {
            if (request.DiscountFixedAmount < 0)
            {
                return Result.Failure<Unit>(Error.Validation("Discount fixed amount cannot be negative."));
            }

            tenant.DiscountFixedAmount = request.DiscountFixedAmount;
        }

        if (request.DiscountCurrency is not null)
        {
            tenant.DiscountCurrency = string.IsNullOrWhiteSpace(request.DiscountCurrency)
                ? null
                : request.DiscountCurrency.Trim().ToUpperInvariant();
        }

        if (request.DiscountCyclesRemaining.HasValue)
        {
            if (request.DiscountCyclesRemaining < 0)
            {
                return Result.Failure<Unit>(Error.Validation("Discount cycles remaining cannot be negative."));
            }

            tenant.DiscountCyclesRemaining = request.DiscountCyclesRemaining;
        }

        if (request.BillingCompReason is not null)
        {
            tenant.BillingCompReason = string.IsNullOrWhiteSpace(request.BillingCompReason)
                ? null
                : request.BillingCompReason.Trim();
        }

        if (request.ClearCustomSaasPrice == true)
        {
            TenantBillingPolicy.ClearCustomSaasPrice(tenant);
        }

        if (request.CustomSaasMonthlyAmount.HasValue)
        {
            if (request.CustomSaasMonthlyAmount <= 0)
            {
                return Result.Failure<Unit>(Error.Validation("Custom SaaS amount must be positive."));
            }

            tenant.CustomSaasMonthlyAmount = request.CustomSaasMonthlyAmount;
        }

        if (request.CustomSaasCurrency is not null)
        {
            tenant.CustomSaasCurrency = string.IsNullOrWhiteSpace(request.CustomSaasCurrency)
                ? null
                : request.CustomSaasCurrency.Trim().ToUpperInvariant();
        }

        if (request.CustomSaasSku is not null)
        {
            tenant.CustomSaasSku = SubscriptionSkuMapping.FromApiValue(request.CustomSaasSku);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogWriter.WriteAsync(new AuditEntry(
            Guid.NewGuid(),
            adminId,
            "tenant.commercial_terms_updated",
            "tenant",
            tenantId.ToString(),
            correlationContext.CorrelationId,
            DateTimeOffset.UtcNow,
            null),
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
