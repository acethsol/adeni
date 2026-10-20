namespace Adeni.Infrastructure.Subscriptions;

using Adeni.Application.Abstractions;
using Adeni.Application.Caching;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Subscriptions;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class SubscriptionBillingOrchestrator(
    AdeniDbContext dbContext,
    ICacheService cache,
    IAuditLogWriter auditLogWriter,
    ICorrelationContext correlationContext,
    ITenantPricingResolver tenantPricingResolver,
    ILogger<SubscriptionBillingOrchestrator> logger) : ISubscriptionBillingOrchestrator
{
    private static readonly HashSet<string> ActivationEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "subscription.create",
        "subscription.enable",
        "invoice.payment_succeeded",
    };

    private static readonly HashSet<string> DeactivationEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "subscription.disable",
        "subscription.not_renew",
        "invoice.payment_failed",
    };

    public async Task<Result<TenantSubscriptionChargeResponse>> ApplyWebhookEventAsync(
        SubscriptionWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default)
    {
        if (webhookEvent.TenantId is not { } tenantId)
        {
            return Result.Failure<TenantSubscriptionChargeResponse>(
                Error.Validation("Subscription webhook missing tenant id."));
        }

        var idempotencyKey = BuildIdempotencyKey(webhookEvent.EventType, webhookEvent.ProviderReference);
        var existing = await dbContext.TenantSubscriptionCharges
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existing is not null)
        {
            logger.LogInformation(
                "Skipping duplicate subscription webhook {Key} for tenant {TenantId}",
                idempotencyKey,
                tenantId);
            return Result.Success(Map(existing));
        }

        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantSubscriptionChargeResponse>(Error.NotFound("Business"));
        }

        TenantBillingLifecycle.ApplyPendingTransitions(tenant, webhookEvent.OccurredAt);

        var targetSku = ResolveSku(webhookEvent.Sku, tenant);
        var country = await tenantPricingResolver.ResolvePrimaryBillingCountryAsync(
            tenantId,
            cancellationToken);
        var pricingBeforeBind = tenantPricingResolver.ResolveForTenant(
            tenant,
            country,
            targetSku,
            webhookEvent.OccurredAt);
        var hadAgreedPrice = tenant.AgreedMonthlyAmount is > 0;

        ApplyBillingTransition(tenant, webhookEvent, targetSku, pricingBeforeBind);

        var (amountMinor, currency) = await ResolveChargeAmountAsync(
            tenant,
            targetSku,
            webhookEvent,
            cancellationToken);

        var charge = new TenantSubscriptionCharge
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IdempotencyKey = idempotencyKey,
            ProviderReference = webhookEvent.ProviderReference,
            EventType = webhookEvent.EventType,
            SubscriptionSku = targetSku,
            AmountMinor = amountMinor,
            Currency = currency,
            Status = NormalizeChargeStatus(webhookEvent.Status),
            OccurredAt = webhookEvent.OccurredAt,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        dbContext.TenantSubscriptionCharges.Add(charge);

        var boundThisEvent = !hadAgreedPrice && tenant.AgreedMonthlyAmount is > 0;

        await dbContext.SaveChangesAsync(cancellationToken);

        if (boundThisEvent)
        {
            await auditLogWriter.WriteAsync(
                new AuditEntry(
                    Guid.NewGuid(),
                    "system:subscription-billing",
                    AuditActions.TenantCommercialBound,
                    "tenant",
                    tenantId.ToString(),
                    correlationContext.CorrelationId,
                    DateTimeOffset.UtcNow,
                    $"{{\"pricingCatalogVersion\":\"{tenant.PricingCatalogVersionCode}\",\"agreedMonthlyAmount\":{tenant.AgreedMonthlyAmount},\"currency\":\"{tenant.AgreedCurrencyCode}\"}}"),
                cancellationToken);
        }

        await cache.RemoveAsync(CacheKeys.TenantProfile(tenantId), cancellationToken);

        var slugs = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => x.Slug)
            .ToListAsync(cancellationToken);

        foreach (var slug in slugs)
        {
            await cache.RemoveAsync(CacheKeys.LocationProfile(slug), cancellationToken);
        }

        await auditLogWriter.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                "system:subscription-billing",
                AuditActions.SubscriptionWebhookApplied,
                "tenant",
                tenantId.ToString(),
                correlationContext.CorrelationId,
                DateTimeOffset.UtcNow,
                $"{{\"eventType\":\"{webhookEvent.EventType}\",\"sku\":\"{SubscriptionSkuMapping.ToApiValue(targetSku)}\"}}"),
            cancellationToken);

        logger.LogInformation(
            "Applied subscription webhook {EventType} for tenant {TenantId} → sku {Sku}",
            webhookEvent.EventType,
            tenantId,
            targetSku);

        return Result.Success(Map(charge));
    }

    public async Task<Result<IReadOnlyList<TenantSubscriptionChargeResponse>>> ListChargesForTenantAsync(
        Guid tenantId,
        string auth0Sub,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return Result.Failure<IReadOnlyList<TenantSubscriptionChargeResponse>>(
                Error.Forbidden("Authentication is required."));
        }

        var hasAccess = await dbContext.BusinessUsers
            .AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Auth0Sub == auth0Sub, cancellationToken);

        if (!hasAccess)
        {
            return Result.Failure<IReadOnlyList<TenantSubscriptionChargeResponse>>(
                Error.Forbidden("You do not have access to this business."));
        }

        return Result.Success(await QueryChargesAsync(tenantId, limit, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<TenantSubscriptionChargeResponse>>> ListChargesForAdminAsync(
        Guid tenantId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(x => x.Id == tenantId, cancellationToken);

        if (!exists)
        {
            return Result.Failure<IReadOnlyList<TenantSubscriptionChargeResponse>>(
                Error.NotFound("Business"));
        }

        return Result.Success(await QueryChargesAsync(tenantId, limit, cancellationToken));
    }

    public async Task<Result<TenantSubscriptionChargeResponse>> CompleteStubCheckoutAsync(
        Guid tenantId,
        string auth0Sub,
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return Result.Failure<TenantSubscriptionChargeResponse>(Error.Forbidden("Authentication is required."));
        }

        var hasAccess = await dbContext.BusinessUsers
            .AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Auth0Sub == auth0Sub, cancellationToken);

        if (!hasAccess)
        {
            return Result.Failure<TenantSubscriptionChargeResponse>(ErrorCodes.PermissionDeniedError());
        }

        if (!TryParseStubReference(providerReference, out var refTenantId, out var skuApi))
        {
            return Result.Failure<TenantSubscriptionChargeResponse>(
                Error.Validation("Invalid stub checkout reference."));
        }

        if (refTenantId != tenantId)
        {
            return Result.Failure<TenantSubscriptionChargeResponse>(ErrorCodes.PermissionDeniedError());
        }

        var webhook = new SubscriptionWebhookEvent(
            "subscription.create",
            providerReference,
            tenantId,
            skuApi,
            "active",
            DateTimeOffset.UtcNow);

        return await ApplyWebhookEventAsync(webhook, cancellationToken);
    }

    private void ApplyBillingTransition(
        Domain.Tenancy.Tenant tenant,
        SubscriptionWebhookEvent webhookEvent,
        SubscriptionSku targetSku,
        TenantPricingResolution pricingBeforeBind)
    {
        if (ActivationEvents.Contains(webhookEvent.EventType)
            || string.Equals(webhookEvent.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            tenant.SubscriptionSku = targetSku;
            TenantBillingLifecycle.MarkPaymentSucceeded(tenant, webhookEvent.OccurredAt);
            TenantCommercialBinding.BindOnFirstPaidActivation(
                tenant,
                pricingBeforeBind,
                webhookEvent.OccurredAt);
            return;
        }

        if (DeactivationEvents.Contains(webhookEvent.EventType))
        {
            if (string.Equals(webhookEvent.EventType, "invoice.payment_failed", StringComparison.OrdinalIgnoreCase))
            {
                TenantBillingLifecycle.MarkPaymentFailed(tenant, webhookEvent.OccurredAt);
            }
            else if (string.Equals(webhookEvent.EventType, "subscription.disable", StringComparison.OrdinalIgnoreCase))
            {
                tenant.BillingStatus = TenantBillingStatus.Suspended;
            }
            else
            {
                tenant.BillingStatus = TenantBillingStatus.Canceled;
            }
        }
    }

    private static SubscriptionSku ResolveSku(string? skuApi, Domain.Tenancy.Tenant tenant)
    {
        if (!string.IsNullOrWhiteSpace(skuApi))
        {
            return SubscriptionSkuMapping.FromApiValue(skuApi);
        }

        return tenant.SubscriptionSku;
    }

    private async Task<(long? AmountMinor, string? Currency)> ResolveChargeAmountAsync(
        Domain.Tenancy.Tenant tenant,
        SubscriptionSku sku,
        SubscriptionWebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        if (webhookEvent.AmountMinor is > 0 && !string.IsNullOrWhiteSpace(webhookEvent.Currency))
        {
            return (webhookEvent.AmountMinor, webhookEvent.Currency);
        }

        var country = await tenantPricingResolver.ResolvePrimaryBillingCountryAsync(tenant.Id, cancellationToken);
        var pricing = tenantPricingResolver.ResolveForTenant(tenant, country, sku);
        var minor = tenantPricingResolver.ToAmountMinor(pricing.EffectiveMonthlyAmountMajor, pricing.Currency);
        return (minor, minor is > 0 ? pricing.Currency : null);
    }

    private async Task<IReadOnlyList<TenantSubscriptionChargeResponse>> QueryChargesAsync(
        Guid tenantId,
        int limit,
        CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 50);

        var rows = await dbContext.TenantSubscriptionCharges
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.OccurredAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    private static string BuildIdempotencyKey(string eventType, string providerReference) =>
        $"{eventType}:{providerReference}".Trim().ToLowerInvariant();

    private static string NormalizeChargeStatus(string status) =>
        string.IsNullOrWhiteSpace(status) ? "received" : status.Trim().ToLowerInvariant();

    private static TenantSubscriptionChargeResponse Map(TenantSubscriptionCharge row) =>
        new(
            row.Id,
            row.TenantId,
            row.ProviderReference,
            row.EventType,
            row.SubscriptionSku is { } sku ? SubscriptionSkuMapping.ToApiValue(sku) : null,
            row.AmountMinor,
            row.Currency,
            row.Status,
            row.OccurredAt);

    private static bool TryParseStubReference(string reference, out Guid tenantId, out string skuApi)
    {
        tenantId = Guid.Empty;
        skuApi = "professional";
        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var parts = reference.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4
            || !parts[0].Equals("stub", StringComparison.OrdinalIgnoreCase)
            || !parts[1].Equals("sub", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Guid.TryParseExact(parts[2], "N", out tenantId))
        {
            return false;
        }

        skuApi = parts[3];
        return true;
    }
}
