namespace Adeni.Infrastructure.Subscriptions;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Routes SaaS subscription billing by tenant market (Lagos → Paystack). Ottawa/Stripe Billing is not wired yet.
/// </summary>
public sealed class MarketDefaultSubscriptionBillingProvider(
    AdeniDbContext dbContext,
    PaystackSubscriptionBillingProvider paystack,
    StubSubscriptionBillingProvider stub,
    ILogger<MarketDefaultSubscriptionBillingProvider> logger) : ISubscriptionBillingProvider
{
    public async Task<Result<SubscriptionCheckoutResponse>> CreateCheckoutAsync(
        CreateSubscriptionCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var marketId = await ResolvePrimaryMarketIdAsync(request.TenantId, cancellationToken);
        if (string.Equals(marketId, "ottawa", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<SubscriptionCheckoutResponse>(
                Error.Validation("Stripe subscription billing for Ottawa is not available yet."));
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

    private async Task<string?> ResolvePrimaryMarketIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsPrimary && x.IsActive)
            .Select(x => x.MarketId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool HasHeader(IReadOnlyDictionary<string, string> headers, string name) =>
        headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value);
}
