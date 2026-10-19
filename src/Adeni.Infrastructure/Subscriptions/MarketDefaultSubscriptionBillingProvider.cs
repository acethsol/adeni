namespace Adeni.Infrastructure.Subscriptions;

using Adeni.Application.Markets;
using Adeni.Application.Finance;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Microsoft.Extensions.Logging;

/// <summary>
/// Routes SaaS subscription billing by tenant market via <see cref="IPaymentProviderRegistry"/>.
/// Stripe Billing for Ottawa is not wired yet.
/// </summary>
public sealed class MarketDefaultSubscriptionBillingProvider(
    IPaymentProviderRegistry registry,
    PaystackSubscriptionBillingProvider paystack,
    StubSubscriptionBillingProvider stub,
    ILogger<MarketDefaultSubscriptionBillingProvider> logger) : ISubscriptionBillingProvider
{
    public async Task<Result<SubscriptionCheckoutResponse>> CreateCheckoutAsync(
        CreateSubscriptionCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var billingKey = await registry.ResolveBillingProviderKeyForTenantAsync(request.TenantId, cancellationToken);
        if (string.Equals(billingKey, MarketPaymentProviders.Stripe, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<SubscriptionCheckoutResponse>(
                Error.Validation("Stripe subscription billing is not available yet for this market."));
        }

        return await paystack.CreateCheckoutAsync(request, cancellationToken);
    }

    public Task<Result<SubscriptionWebhookEvent>> ParseWebhookAsync(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        if (HasHeader(headers, "x-paystack-signature"))
        {
            return paystack.ParseWebhookAsync(rawBody, headers, cancellationToken);
        }

        logger.LogInformation("Subscription webhook without Paystack signature — using stub parser (local/dev).");
        return stub.ParseWebhookAsync(rawBody, headers, cancellationToken);
    }

    private static bool HasHeader(IReadOnlyDictionary<string, string> headers, string name) =>
        headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value);
}
