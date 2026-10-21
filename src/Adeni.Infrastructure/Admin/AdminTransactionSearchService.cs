namespace Adeni.Infrastructure.Admin;

using Adeni.Application.Admin;
using Adeni.Application.Common;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Domain.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class AdminTransactionSearchService(AdeniDbContext dbContext) : IAdminTransactionSearchService
{
    private const int MaxMergedHits = 1000;

    private sealed record Hit(
        string Kind,
        Guid Id,
        DateTimeOffset OccurredAt,
        Guid TenantId,
        string? ProviderReference,
        long? AmountMinor,
        decimal? Amount,
        string Status);

    public async Task<Result<AdminPagedResult<AdminTransactionSearchItem>>> SearchAsync(
        string? query,
        string? kind,
        int page,
        int pageSize,
        string? sortBy = null,
        ListSortDirection? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        var paging = AdminPaging.Normalize(page, pageSize);
        if (paging.IsFailure)
        {
            return Result.Failure<AdminPagedResult<AdminTransactionSearchItem>>(paging.Error);
        }

        var (effectivePage, effectivePageSize) = paging.Value;

        var trimmed = query?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return await ListRecentAsync(
                kind,
                effectivePage,
                effectivePageSize,
                sortBy,
                sortDirection,
                cancellationToken);
        }

        if (trimmed.Length < 2)
        {
            return Result.Failure<AdminPagedResult<AdminTransactionSearchItem>>(
                Error.Validation("Enter at least 2 characters (Paystack reference, transaction id, or business name)."));
        }

        var needle = trimmed.ToLowerInvariant();
        _ = Guid.TryParse(trimmed, out var exactGuid);

        var tenantIdsByName = await dbContext.Tenants
            .AsNoTracking()
            .Where(t =>
                t.Name.ToLower().Contains(needle)
                || (exactGuid != Guid.Empty && t.Id == exactGuid))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var includeCommerce = MatchesKindFilter(kind, "commerce");
        var includeSaas = MatchesKindFilter(kind, "saas");

        var hits = new List<Hit>();

        if (includeCommerce)
        {
            var commerceHits = await dbContext.PaymentIntents
                .AsNoTracking()
                .Where(x =>
                    (exactGuid != Guid.Empty && (x.Id == exactGuid || x.TenantId == exactGuid || x.BookingId == exactGuid))
                    || x.ProviderReference.ToLower().Contains(needle)
                    || (x.IdempotencyKey != null && x.IdempotencyKey.ToLower().Contains(needle))
                    || tenantIdsByName.Contains(x.TenantId))
                .Select(x => new Hit(
                    "commerce",
                    x.Id,
                    x.UpdatedAt,
                    x.TenantId,
                    x.ProviderReference,
                    null,
                    x.Amount,
                    x.Status.ToString()))
                .ToListAsync(cancellationToken);
            hits.AddRange(commerceHits);
        }

        if (includeSaas)
        {
            var saasHits = await dbContext.TenantSubscriptionCharges
                .AsNoTracking()
                .Where(x =>
                    (exactGuid != Guid.Empty && (x.Id == exactGuid || x.TenantId == exactGuid))
                    || x.ProviderReference.ToLower().Contains(needle)
                    || x.IdempotencyKey.ToLower().Contains(needle)
                    || tenantIdsByName.Contains(x.TenantId))
                .Select(x => new Hit(
                    "saas",
                    x.Id,
                    x.OccurredAt,
                    x.TenantId,
                    x.ProviderReference,
                    x.AmountMinor,
                    null,
                    x.Status))
                .ToListAsync(cancellationToken);
            hits.AddRange(saasHits);
        }

        if (hits.Count > MaxMergedHits)
        {
            return Result.Failure<AdminPagedResult<AdminTransactionSearchItem>>(
                Error.Validation("Too many matches. Use a Paystack reference or full transaction id."));
        }

        return await BuildPageAsync(
            hits,
            effectivePage,
            effectivePageSize,
            sortBy,
            sortDirection,
            cancellationToken);
    }

    private async Task<Result<AdminPagedResult<AdminTransactionSearchItem>>> ListRecentAsync(
        string? kind,
        int page,
        int pageSize,
        string? sortBy,
        ListSortDirection? sortDirection,
        CancellationToken cancellationToken)
    {
        var includeCommerce = MatchesKindFilter(kind, "commerce");
        var includeSaas = MatchesKindFilter(kind, "saas");
        var hits = new List<Hit>();

        if (includeCommerce)
        {
            var commerceHits = await dbContext.PaymentIntents
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .Take(MaxMergedHits)
                .Select(x => new Hit(
                    "commerce",
                    x.Id,
                    x.UpdatedAt,
                    x.TenantId,
                    x.ProviderReference,
                    null,
                    x.Amount,
                    x.Status.ToString()))
                .ToListAsync(cancellationToken);
            hits.AddRange(commerceHits);
        }

        if (includeSaas)
        {
            var saasHits = await dbContext.TenantSubscriptionCharges
                .AsNoTracking()
                .OrderByDescending(x => x.OccurredAt)
                .Take(MaxMergedHits)
                .Select(x => new Hit(
                    "saas",
                    x.Id,
                    x.OccurredAt,
                    x.TenantId,
                    x.ProviderReference,
                    x.AmountMinor,
                    null,
                    x.Status))
                .ToListAsync(cancellationToken);
            hits.AddRange(saasHits);
        }

        return await BuildPageAsync(hits, page, pageSize, sortBy, sortDirection, cancellationToken);
    }

    private async Task<Result<AdminPagedResult<AdminTransactionSearchItem>>> BuildPageAsync(
        List<Hit> hits,
        int page,
        int pageSize,
        string? sortBy,
        ListSortDirection? sortDirection,
        CancellationToken cancellationToken)
    {
        var field = sortBy ?? AdminTransactionListSort.DefaultField;
        var direction = sortDirection ?? AdminTransactionListSort.DefaultDirection;
        var ordered = await OrderHitsAsync(hits, field, direction, cancellationToken);

        var totalCount = ordered.Count;
        var pageHits = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var tenantNames = await dbContext.Tenants
            .AsNoTracking()
            .Select(t => new { t.Id, t.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var commerceIds = pageHits.Where(x => x.Kind == "commerce").Select(x => x.Id).ToList();
        var saasIds = pageHits.Where(x => x.Kind == "saas").Select(x => x.Id).ToList();

        var commerceById = commerceIds.Count == 0
            ? new Dictionary<Guid, PaymentIntentRecord>()
            : await dbContext.PaymentIntents
                .AsNoTracking()
                .Where(x => commerceIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        var saasById = saasIds.Count == 0
            ? new Dictionary<Guid, Domain.Subscriptions.TenantSubscriptionCharge>()
            : await dbContext.TenantSubscriptionCharges
                .AsNoTracking()
                .Where(x => saasIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        var items = pageHits
            .Select(hit => MapHit(hit, tenantNames, commerceById, saasById))
            .ToList();

        return Result.Success(new AdminPagedResult<AdminTransactionSearchItem>(
            items,
            page,
            pageSize,
            totalCount));
    }

    private async Task<List<Hit>> OrderHitsAsync(
        List<Hit> hits,
        string field,
        ListSortDirection direction,
        CancellationToken cancellationToken)
    {
        var desc = ListSort.IsDescending(direction);
        var tenantIds = hits.Select(x => x.TenantId).Where(x => x != Guid.Empty).Distinct().ToList();
        var tenantNames = tenantIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Tenants
                .AsNoTracking()
                .Where(t => tenantIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        IOrderedEnumerable<Hit> ordered = field switch
        {
            "type" => desc
                ? hits.OrderByDescending(x => x.Kind, StringComparer.OrdinalIgnoreCase)
                : hits.OrderBy(x => x.Kind, StringComparer.OrdinalIgnoreCase),
            "business" => desc
                ? hits.OrderByDescending(x => tenantNames.GetValueOrDefault(x.TenantId) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                : hits.OrderBy(x => tenantNames.GetValueOrDefault(x.TenantId) ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            "reference" => desc
                ? hits.OrderByDescending(x => x.ProviderReference ?? x.Id.ToString(), StringComparer.OrdinalIgnoreCase)
                : hits.OrderBy(x => x.ProviderReference ?? x.Id.ToString(), StringComparer.OrdinalIgnoreCase),
            "amount" => desc
                ? hits.OrderByDescending(x => x.AmountMinor ?? (long)((x.Amount ?? 0m) * 100m))
                : hits.OrderBy(x => x.AmountMinor ?? (long)((x.Amount ?? 0m) * 100m)),
            "status" => desc
                ? hits.OrderByDescending(x => x.Status, StringComparer.OrdinalIgnoreCase)
                : hits.OrderBy(x => x.Status, StringComparer.OrdinalIgnoreCase),
            _ => desc
                ? hits.OrderByDescending(x => x.OccurredAt)
                : hits.OrderBy(x => x.OccurredAt),
        };

        return ordered
            .ThenBy(x => x.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id)
            .ToList();
    }

    private static bool MatchesKindFilter(string? kind, string target)
    {
        if (string.IsNullOrWhiteSpace(kind) || kind.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return kind.Equals(target, StringComparison.OrdinalIgnoreCase);
    }

    private static AdminTransactionSearchItem MapHit(
        Hit hit,
        IReadOnlyDictionary<Guid, string> tenantNames,
        IReadOnlyDictionary<Guid, PaymentIntentRecord> commerceById,
        IReadOnlyDictionary<Guid, Domain.Subscriptions.TenantSubscriptionCharge> saasById)
    {
        if (hit.Kind == "commerce" && commerceById.TryGetValue(hit.Id, out var payment))
        {
            return new AdminTransactionSearchItem(
                "commerce",
                payment.Id,
                payment.TenantId,
                tenantNames.GetValueOrDefault(payment.TenantId) ?? "—",
                string.IsNullOrWhiteSpace(payment.ProviderReference) ? null : payment.ProviderReference,
                null,
                null,
                null,
                payment.Amount,
                payment.PlatformFeeAmount,
                payment.Currency,
                payment.Status.ToString(),
                payment.BookingId,
                payment.UpdatedAt);
        }

        if (hit.Kind == "saas" && saasById.TryGetValue(hit.Id, out var charge))
        {
            return new AdminTransactionSearchItem(
                "saas",
                charge.Id,
                charge.TenantId,
                tenantNames.GetValueOrDefault(charge.TenantId) ?? "—",
                string.IsNullOrWhiteSpace(charge.ProviderReference) ? null : charge.ProviderReference,
                charge.EventType,
                charge.SubscriptionSku is { } sku ? SubscriptionSkuMapping.ToApiValue(sku) : null,
                charge.AmountMinor,
                null,
                null,
                charge.Currency ?? "NGN",
                charge.Status,
                null,
                charge.OccurredAt);
        }

        return new AdminTransactionSearchItem(
            hit.Kind,
            hit.Id,
            Guid.Empty,
            "—",
            null,
            null,
            null,
            null,
            null,
            null,
            "NGN",
            "unknown",
            null,
            hit.OccurredAt);
    }
}
