namespace Adeni.Infrastructure.Payments;

using Adeni.Application.Payments;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Routes payment operations by tenant market (Lagos → Paystack, Ottawa → Stripe)
/// and by webhook signature headers.
/// </summary>
public sealed class MarketDefaultPaymentProvider(
    AdeniDbContext dbContext,
    PaystackPaymentProvider paystack,
    StripePaymentProvider stripe,
    ILogger<MarketDefaultPaymentProvider> logger) : IPaymentProvider
{
    public string ProviderName => "MarketDefault";

    public async Task<Result<ProviderInitializeResult>> InitializeAsync(
        ProviderInitializeRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await ResolveProviderForTenantAsync(request.TenantId, cancellationToken);
        return await provider.InitializeAsync(request, cancellationToken);
    }

    public Task<Result<ProviderPaymentStatus>> GetProviderStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default) =>
        ResolveProviderForReference(providerReference)
            .GetProviderStatusAsync(providerReference, cancellationToken);

    public Task<Result<PaymentWebhookPayload>> ParseWebhookAsync(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        if (HasHeader(headers, "Stripe-Signature") || HasHeader(headers, "stripe-signature"))
        {
            return stripe.ParseWebhookAsync(rawBody, headers, cancellationToken);
        }

        if (HasHeader(headers, "x-paystack-signature") || HasHeader(headers, "X-Paystack-Signature"))
        {
            return paystack.ParseWebhookAsync(rawBody, headers, cancellationToken);
        }

        logger.LogWarning("MarketDefault webhook missing Stripe-Signature and x-paystack-signature.");
        return Task.FromResult(Result.Failure<PaymentWebhookPayload>(ErrorCodes.PaymentWebhookInvalidError()));
    }

    public Task<Result<ProviderRefundResult>> RefundAsync(
        string providerReference,
        decimal? amount,
        CancellationToken cancellationToken = default) =>
        ResolveProviderForReference(providerReference)
            .RefundAsync(providerReference, amount, cancellationToken);

    private async Task<IPaymentProvider> ResolveProviderForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var marketId = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsPrimary)
            .Select(x => x.MarketId)
            .FirstOrDefaultAsync(cancellationToken);

        marketId ??= await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.MarketId)
            .FirstOrDefaultAsync(cancellationToken);

        return ResolveProviderForMarket(marketId);
    }

    private IPaymentProvider ResolveProviderForMarket(string? marketId)
    {
        if (string.Equals(marketId, "ottawa", StringComparison.OrdinalIgnoreCase))
        {
            return stripe;
        }

        // Lagos and unknown markets default to Paystack (Africa wedge).
        return paystack;
    }

    private IPaymentProvider ResolveProviderForReference(string providerReference)
    {
        if (providerReference.StartsWith("cs_", StringComparison.Ordinal)
            || providerReference.StartsWith("pi_", StringComparison.Ordinal)
            || providerReference.StartsWith("stripe_", StringComparison.OrdinalIgnoreCase))
        {
            return stripe;
        }

        return paystack;
    }

    private static bool HasHeader(IReadOnlyDictionary<string, string> headers, string name) =>
        headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value);
}
