namespace Adeni.Infrastructure.Admin;

using Adeni.Application.Reporting;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class AdminReportExportQueryService(AdeniDbContext dbContext) : IAdminReportExportQueryService
{
    private const int MaxExportRows = 5000;

    public async Task<Result<IReadOnlyList<AdminBusinessDirectoryExportRow>>> GetBusinessDirectoryAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
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

        var rows = await query
            .OrderBy(t => t.Name)
            .Take(MaxExportRows)
            .Select(t => new AdminBusinessDirectoryExportRow(
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
                t.Status.ToString(),
                SubscriptionSkuMapping.ToApiValue(t.SubscriptionSku),
                TenantBillingStatusMapping.ToApiValue(t.BillingStatus),
                t.TrialEndsAt,
                t.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AdminBusinessDirectoryExportRow>>(rows);
    }

    public async Task<Result<IReadOnlyList<AdminPendingVerificationExportRow>>> GetPendingVerificationsAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
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

        var tenants = await query
            .OrderBy(t => t.CreatedAt)
            .Take(MaxExportRows)
            .Select(t => new { t.Id, t.Name, t.CreatedAt })
            .ToListAsync(cancellationToken);

        if (tenants.Count == 0)
        {
            return Result.Success<IReadOnlyList<AdminPendingVerificationExportRow>>([]);
        }

        var tenantIds = tenants.Select(x => x.Id).ToList();
        var locations = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => tenantIds.Contains(x.TenantId) && x.IsActive)
            .ToListAsync(cancellationToken);

        var docGroups = await dbContext.VerificationDocuments
            .AsNoTracking()
            .Where(x => tenantIds.Contains(x.TenantId))
            .GroupBy(x => x.TenantId)
            .Select(g => new
            {
                TenantId = g.Key,
                Count = g.Count(),
                LatestSubmittedAt = g.Max(x => x.SubmittedAt),
            })
            .ToDictionaryAsync(x => x.TenantId, cancellationToken);

        var rows = tenants.Select(tenant =>
        {
            var primary = locations
                .Where(x => x.TenantId == tenant.Id)
                .OrderByDescending(x => x.IsPrimary)
                .FirstOrDefault();

            docGroups.TryGetValue(tenant.Id, out var docGroup);

            return new AdminPendingVerificationExportRow(
                tenant.Id,
                tenant.Name,
                primary?.Slug ?? string.Empty,
                primary?.MarketId ?? string.Empty,
                docGroup?.LatestSubmittedAt ?? tenant.CreatedAt,
                docGroup?.Count ?? 0);
        }).ToList();

        return Result.Success<IReadOnlyList<AdminPendingVerificationExportRow>>(rows);
    }

    public async Task<Result<IReadOnlyList<AdminTransactionExportRow>>> GetTransactionsAsync(
        string? search,
        string? kind,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var includeCommerce = MatchesKindFilter(kind, "commerce");
        var includeSaas = MatchesKindFilter(kind, "saas");
        var rows = new List<AdminTransactionExportRow>();

        var tenantNames = await dbContext.Tenants
            .AsNoTracking()
            .Select(t => new { t.Id, t.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var trimmed = search?.Trim();
        var needle = trimmed?.ToLowerInvariant();
        _ = Guid.TryParse(trimmed, out var exactGuid);

        List<Guid>? tenantIdsByName = null;
        if (!string.IsNullOrWhiteSpace(needle))
        {
            tenantIdsByName = await dbContext.Tenants
                .AsNoTracking()
                .Where(t =>
                    t.Name.ToLower().Contains(needle)
                    || (exactGuid != Guid.Empty && t.Id == exactGuid))
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);
        }

        if (includeCommerce)
        {
            var commerceQuery = dbContext.PaymentIntents
                .AsNoTracking()
                .Where(x => x.UpdatedAt >= from && x.UpdatedAt <= to);

            if (!string.IsNullOrWhiteSpace(needle))
            {
                commerceQuery = commerceQuery.Where(x =>
                    (exactGuid != Guid.Empty && (x.Id == exactGuid || x.TenantId == exactGuid || x.BookingId == exactGuid))
                    || x.ProviderReference.ToLower().Contains(needle)
                    || (x.IdempotencyKey != null && x.IdempotencyKey.ToLower().Contains(needle))
                    || (tenantIdsByName != null && tenantIdsByName.Contains(x.TenantId)));
            }

            var commerceRows = await commerceQuery
                .OrderByDescending(x => x.UpdatedAt)
                .Take(MaxExportRows)
                .Select(x => new AdminTransactionExportRow(
                    "commerce",
                    x.Id,
                    x.TenantId,
                    string.Empty,
                    x.ProviderReference,
                    null,
                    null,
                    null,
                    x.Currency,
                    x.Status.ToString(),
                    x.UpdatedAt))
                .ToListAsync(cancellationToken);

            foreach (var row in commerceRows)
            {
                rows.Add(row with
                {
                    TenantName = tenantNames.GetValueOrDefault(row.TenantId, string.Empty),
                });
            }
        }

        if (includeSaas)
        {
            var saasQuery = dbContext.TenantSubscriptionCharges
                .AsNoTracking()
                .Where(x => x.OccurredAt >= from && x.OccurredAt <= to);

            if (!string.IsNullOrWhiteSpace(needle))
            {
                saasQuery = saasQuery.Where(x =>
                    (exactGuid != Guid.Empty && (x.Id == exactGuid || x.TenantId == exactGuid))
                    || (x.ProviderReference != null && x.ProviderReference.ToLower().Contains(needle))
                    || (tenantIdsByName != null && tenantIdsByName.Contains(x.TenantId)));
            }

            var saasRows = await saasQuery
                .OrderByDescending(x => x.OccurredAt)
                .Take(MaxExportRows)
                .Select(x => new AdminTransactionExportRow(
                    "saas",
                    x.Id,
                    x.TenantId,
                    string.Empty,
                    x.ProviderReference,
                    x.EventType,
                    x.SubscriptionSku != null
                        ? SubscriptionSkuMapping.ToApiValue(x.SubscriptionSku.Value)
                        : null,
                    x.AmountMinor,
                    x.Currency ?? string.Empty,
                    x.Status,
                    x.OccurredAt))
                .ToListAsync(cancellationToken);

            foreach (var row in saasRows)
            {
                rows.Add(row with
                {
                    TenantName = tenantNames.GetValueOrDefault(row.TenantId, string.Empty),
                });
            }
        }

        var ordered = rows
            .OrderByDescending(x => x.OccurredAt)
            .Take(MaxExportRows)
            .ToList();

        return Result.Success<IReadOnlyList<AdminTransactionExportRow>>(ordered);
    }

    public async Task<Result<IReadOnlyList<AdminPricingCatalogEntryExportRow>>> GetPricingCatalogEntriesAsync(
        string catalogVersionCode,
        CancellationToken cancellationToken = default)
    {
        var code = catalogVersionCode?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure<IReadOnlyList<AdminPricingCatalogEntryExportRow>>(
                Error.Validation("catalogVersionCode is required for pricing catalog exports."));
        }

        var version = await dbContext.PricingCatalogVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Code == code, cancellationToken);

        if (version is null)
        {
            return Result.Failure<IReadOnlyList<AdminPricingCatalogEntryExportRow>>(
                Error.NotFound("Pricing catalog version not found."));
        }

        var entries = await dbContext.PricingCatalogEntries
            .AsNoTracking()
            .Where(e => e.PricingCatalogVersionId == version.Id)
            .OrderBy(e => e.CountryCode)
            .ThenBy(e => e.SubscriptionSku)
            .Take(MaxExportRows)
            .Select(e => new AdminPricingCatalogEntryExportRow(
                code,
                e.CountryCode,
                SubscriptionSkuMapping.ToApiValue(e.SubscriptionSku),
                e.CurrencyCode,
                e.MonthlyAmountMajor,
                e.PlatformFeePercent,
                e.IsSelfServeEnabled))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AdminPricingCatalogEntryExportRow>>(entries);
    }

    private static bool MatchesKindFilter(string? kind, string target) =>
        string.IsNullOrWhiteSpace(kind)
        || string.Equals(kind, "all", StringComparison.OrdinalIgnoreCase)
        || string.Equals(kind, target, StringComparison.OrdinalIgnoreCase);
}
