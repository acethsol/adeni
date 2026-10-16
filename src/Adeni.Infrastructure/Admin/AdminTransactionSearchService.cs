namespace Adeni.Infrastructure.Admin;

using Adeni.Application.Admin;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Domain.Payments;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class AdminTransactionSearchService(AdeniDbContext dbContext) : IAdminTransactionSearchService
{
    private const int MaxMergedHits = 1000;

    private sealed record Hit(string Kind, Guid Id, DateTimeOffset OccurredAt);

    public async Task<Result<AdminPagedResult<AdminTransactionSearchItem>>> SearchAsync(
        string? query,
        string? kind,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var trimmed = query?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length < 2)
        {
            return Result.Failure<AdminPagedResult<AdminTransactionSearchItem>>(
                Error.Validation("Enter at least 2 characters (Paystack reference, transaction id, or business name)."));
        }

        var paging = AdminPaging.Normalize(page, pageSize);
        if (paging.IsFailure)
        {
            return Result.Failure<AdminPagedResult<AdminTransactionSearchItem>>(paging.Error);
        }

        var (effectivePage, effectivePageSize) = paging.Value;
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
                .Select(x => new Hit("commerce", x.Id, x.UpdatedAt))
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
                .Select(x => new Hit("saas", x.Id, x.OccurredAt))
                .ToListAsync(cancellationToken);
            hits.AddRange(saasHits);
        }

        if (hits.Count > MaxMergedHits)
        {
            return Result.Failure<AdminPagedResult<AdminTransactionSearchItem>>(
                Error.Validation("Too many matches. Use a Paystack reference or full transaction id."));
        }

        var ordered = hits
            .OrderByDescending(x => x.OccurredAt)
            .ThenBy(x => x.Kind, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalCount = ordered.Count;
        var pageHits = ordered
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
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
            effectivePage,
            effectivePageSize,
            totalCount));
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
