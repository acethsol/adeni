namespace Adeni.Infrastructure.Admin;

using System.Text.Json;
using Adeni.Application.Abstractions;
using Adeni.Application.Admin;
using Adeni.Application.Common;
using Adeni.Application.Caching;
using Adeni.Application.Notifications;
using Adeni.Application.Storage;
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
    ICacheService cache,
    ITenantPricingResolver tenantPricingResolver,
    IFileStorage fileStorage,
    IVerificationLifecycleNotifier verificationLifecycleNotifier) : IAdminBusinessService
{
    public async Task<Result<AdminPagedResult<PendingBusinessResponse>>> ListPendingVerificationsAsync(
        string? search,
        int page,
        int pageSize,
        string? sortBy = null,
        ListSortDirection? sortDirection = null,
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
        var field = sortBy ?? AdminPendingVerificationListSort.DefaultField;
        var direction = sortDirection ?? AdminPendingVerificationListSort.DefaultDirection;
        var sorted = ApplyPendingSort(query, field, direction);
        var tenants = await sorted
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
                    x.Id,
                    x.DocumentType.ToString().ToLowerInvariant(),
                    x.ReferenceNumber,
                    x.SubmittedAt,
                    !string.IsNullOrWhiteSpace(x.StorageFileKey)))
                .ToList();

            DateTimeOffset? submittedAt = docs.Count > 0
                ? docs.Max(x => x.SubmittedAt)
                : null;

            return new PendingBusinessResponse(
                tenant.Id,
                tenant.Name,
                primary?.Slug ?? string.Empty,
                primary?.MarketId ?? string.Empty,
                tenant.Status,
                tenant.CreatedAt,
                submittedAt,
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
        string? sortBy = null,
        ListSortDirection? sortDirection = null,
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
        var field = sortBy ?? AdminBusinessListSort.DefaultField;
        var direction = sortDirection ?? AdminBusinessListSort.DefaultDirection;
        var sorted = ApplyBusinessSort(query, field, direction);
        var items = await sorted
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

    public async Task<Result<AdminBusinessSummaryResponse>> GetBusinessAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
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
                SubscriptionSkuMapping.ToApiValue(t.SubscriptionSku),
                TenantBillingStatusMapping.ToApiValue(t.BillingStatus),
                t.TrialEndsAt,
                t.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return item is null
            ? Result.Failure<AdminBusinessSummaryResponse>(Error.NotFound("Business"))
            : Result.Success(item);
    }

    public async Task<AdminBusinessDirectoryStats> GetDirectoryStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var statusRows = await dbContext.Tenants
            .AsNoTracking()
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var skuRows = await dbContext.Tenants
            .AsNoTracking()
            .GroupBy(t => t.SubscriptionSku)
            .Select(g => new { Sku = g.Key, Count = g.Count() })
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
            skuRows
                .OrderBy(x => x.Sku)
                .Select(x => new AdminSkuCount(SubscriptionSkuMapping.ToApiValue(x.Sku), x.Count))
                .ToList());
    }

    public async Task<Result<AdminTenantCommercialTermsResponse>> GetTenantCommercialTermsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<AdminTenantCommercialTermsResponse>(Error.NotFound("Business"));
        }

        var country = await tenantPricingResolver.ResolvePrimaryBillingCountryAsync(tenantId, cancellationToken);
        var pricing = tenantPricingResolver.ResolveForTenant(tenant, country);
        return Result.Success(AdminCommercialTermsMapper.ToResponse(tenant, pricing));
    }

    public async Task<Result<IReadOnlyList<AdminTenantCommercialAuditEntry>>> ListTenantCommercialAuditAsync(
        Guid tenantId,
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Id == tenantId, cancellationToken);

        if (!exists)
        {
            return Result.Failure<IReadOnlyList<AdminTenantCommercialAuditEntry>>(Error.NotFound("Business"));
        }

        limit = Math.Clamp(limit, 1, 100);
        var tenantKey = tenantId.ToString();
        var commercialActions = new[]
        {
            "tenant.commercial_terms_updated",
            AuditActions.TenantCommercialBound,
            AuditActions.SubscriptionWebhookApplied,
        };

        var rows = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(x =>
                x.EntityType == "tenant"
                && x.EntityId == tenantKey
                && commercialActions.Contains(x.Action))
            .OrderByDescending(x => x.OccurredAt)
            .Take(limit)
            .Select(x => new AdminTenantCommercialAuditEntry(
                x.Id,
                x.ActorId,
                x.Action,
                x.OccurredAt,
                x.MetadataJson))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AdminTenantCommercialAuditEntry>>(rows);
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

        var beforeSnapshot = AdminCommercialTermsSnapshot.FromTenant(tenant);
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

        var afterSnapshot = AdminCommercialTermsSnapshot.FromTenant(tenant);
        var commercialChanged = !beforeSnapshot.EqualsCommercialTerms(afterSnapshot)
            || request.EndTrial == true
            || request.TrialDays.HasValue
            || request.ClearDiscounts == true
            || request.ClearCustomSaasPrice == true;

        var changeReason = ResolveChangeReason(request);
        if (commercialChanged && string.IsNullOrWhiteSpace(changeReason))
        {
            return Result.Failure<Unit>(
                Error.Validation("A change reason is required when updating commercial terms."));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var auditMetadata = commercialChanged
            ? JsonSerializer.Serialize(new
            {
                changeReason,
                before = beforeSnapshot,
                after = afterSnapshot,
            })
            : null;

        await auditLogWriter.WriteAsync(new AuditEntry(
            Guid.NewGuid(),
            adminId,
            "tenant.commercial_terms_updated",
            "tenant",
            tenantId.ToString(),
            correlationContext.CorrelationId,
            DateTimeOffset.UtcNow,
            auditMetadata),
            cancellationToken);

        return Result.Success(Unit.Value);
    }

    private static string? ResolveChangeReason(SetTenantCommercialTermsRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ChangeReason))
        {
            return request.ChangeReason.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.BillingCompReason))
        {
            return request.BillingCompReason.Trim();
        }

        return null;
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

        var trimmed = reason.Trim();
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Business"));
        }

        tenant.VerificationRejectionReason = trimmed;
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = await UpdateStatusAsync(
            tenantId,
            adminId,
            TenantStatus.Rejected,
            AuditActions.BusinessRejected,
            setVerifiedAt: false,
            cancellationToken,
            trimmed);

        if (result.IsSuccess)
        {
            await verificationLifecycleNotifier.NotifyRejectedAsync(tenantId, cancellationToken);
        }

        return result;
    }

    public async Task<Result<VerificationDocumentDownloadResponse>> GetVerificationDocumentDownloadUrlAsync(
        Guid tenantId,
        Guid documentId,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adminId))
        {
            return Result.Failure<VerificationDocumentDownloadResponse>(Error.Forbidden("Authentication is required."));
        }

        var document = await dbContext.VerificationDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == documentId && x.TenantId == tenantId, cancellationToken);

        if (document is null || string.IsNullOrWhiteSpace(document.StorageFileKey))
        {
            return Result.Failure<VerificationDocumentDownloadResponse>(Error.NotFound("Verification document file"));
        }

        var ttl = TimeSpan.FromMinutes(15);
        var url = await fileStorage.GetDownloadUrlAsync(document.StorageFileKey, cancellationToken);

        await auditLogWriter.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                adminId,
                "verification_document.downloaded",
                "verification_document",
                documentId.ToString(),
                correlationContext.CorrelationId,
                DateTimeOffset.UtcNow,
                $"{{\"tenantId\":\"{tenantId:D}\"}}"),
            cancellationToken);

        return Result.Success(new VerificationDocumentDownloadResponse(url, DateTimeOffset.UtcNow.Add(ttl)));
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
            tenant.VerificationRejectionReason = null;
            if (tenant.TrialEndsAt is null && tenant.BillingStatus != TenantBillingStatus.Active)
            {
                TenantBillingPolicy.StartDefaultTrial(tenant, DateTimeOffset.UtcNow);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (status == TenantStatus.Verified)
        {
            await verificationLifecycleNotifier.NotifyApprovedAsync(tenantId, cancellationToken);
        }

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

    private IOrderedQueryable<Tenant> ApplyPendingSort(
        IQueryable<Tenant> query,
        string field,
        ListSortDirection direction)
    {
        var desc = ListSort.IsDescending(direction);
        return field switch
        {
            "business" => desc
                ? query.OrderByDescending(t => t.Name)
                : query.OrderBy(t => t.Name),
            "market" => desc
                ? query.OrderByDescending(t =>
                    dbContext.BusinessLocations
                        .Where(loc => loc.TenantId == t.Id && loc.IsActive)
                        .OrderByDescending(loc => loc.IsPrimary)
                        .Select(loc => loc.MarketId)
                        .FirstOrDefault() ?? string.Empty)
                : query.OrderBy(t =>
                    dbContext.BusinessLocations
                        .Where(loc => loc.TenantId == t.Id && loc.IsActive)
                        .OrderByDescending(loc => loc.IsPrimary)
                        .Select(loc => loc.MarketId)
                        .FirstOrDefault() ?? string.Empty),
            "docs" => desc
                ? query.OrderByDescending(t =>
                    dbContext.VerificationDocuments.Count(doc => doc.TenantId == t.Id))
                : query.OrderBy(t =>
                    dbContext.VerificationDocuments.Count(doc => doc.TenantId == t.Id)),
            "submitted" => desc
                ? query.OrderByDescending(t =>
                    dbContext.VerificationDocuments
                        .Where(doc => doc.TenantId == t.Id)
                        .Max(doc => (DateTimeOffset?)doc.SubmittedAt) ?? t.CreatedAt)
                : query.OrderBy(t =>
                    dbContext.VerificationDocuments
                        .Where(doc => doc.TenantId == t.Id)
                        .Max(doc => (DateTimeOffset?)doc.SubmittedAt) ?? t.CreatedAt),
            _ => desc
                ? query.OrderByDescending(t => t.CreatedAt)
                : query.OrderBy(t => t.CreatedAt),
        };
    }

    private static IOrderedQueryable<Tenant> ApplyBusinessSort(
        IQueryable<Tenant> query,
        string field,
        ListSortDirection direction)
    {
        var desc = ListSort.IsDescending(direction);
        return field switch
        {
            "status" => desc
                ? query.OrderByDescending(t => t.Status)
                : query.OrderBy(t => t.Status),
            "plan" => desc
                ? query.OrderByDescending(t => t.SubscriptionSku)
                : query.OrderBy(t => t.SubscriptionSku),
            "billing" => desc
                ? query.OrderByDescending(t => t.BillingStatus)
                : query.OrderBy(t => t.BillingStatus),
            _ => desc
                ? query.OrderByDescending(t => t.Name)
                : query.OrderBy(t => t.Name),
        };
    }
}
