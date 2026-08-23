namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Common;
using Adeni.Domain.Subscriptions;

/// <summary>
/// Provider-agnostic subscription billing port. Sprint 16 stub only — Paystack Subscriptions in Sprint 17+.
/// </summary>
public sealed record CreateSubscriptionCheckoutRequest(
    Guid TenantId,
    SubscriptionTier TargetTier,
    string CustomerEmail,
    string SuccessUrl,
    string CancelUrl);

public sealed record SubscriptionCheckoutResponse(
    string CheckoutUrl,
    string ProviderReference,
    string Status);

/// <summary>
/// Normalized webhook payload shape for subscription lifecycle events (documented in docs/subscription-billing.md).
/// </summary>
public sealed record SubscriptionWebhookEvent(
    string EventType,
    string ProviderReference,
    Guid? TenantId,
    string? Tier,
    string Status,
    DateTimeOffset OccurredAt);

public interface ISubscriptionBillingProvider
{
    Task<Result<SubscriptionCheckoutResponse>> CreateCheckoutAsync(
        CreateSubscriptionCheckoutRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<SubscriptionWebhookEvent>> ParseWebhookAsync(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}
