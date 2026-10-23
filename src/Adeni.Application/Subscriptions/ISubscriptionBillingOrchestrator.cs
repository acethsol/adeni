namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Common;

public sealed record TenantSubscriptionChargeResponse(
    Guid Id,
    Guid TenantId,
    string ProviderReference,
    string EventType,
    string? SubscriptionSku,
    long? AmountMinor,
    string? Currency,
    string Status,
    DateTimeOffset OccurredAt);

public interface ISubscriptionBillingOrchestrator
{
    Task RecordWebhookReceivedAsync(CancellationToken cancellationToken = default);

    Task<Result<TenantSubscriptionChargeResponse>> ApplyWebhookEventAsync(
        SubscriptionWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TenantSubscriptionChargeResponse>>> ListChargesForTenantAsync(
        Guid tenantId,
        string auth0Sub,
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TenantSubscriptionChargeResponse>>> ListChargesForAdminAsync(
        Guid tenantId,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>Development/stub: complete checkout without provider webhook.</summary>
    Task<Result<TenantSubscriptionChargeResponse>> CompleteStubCheckoutAsync(
        Guid tenantId,
        string auth0Sub,
        string providerReference,
        CancellationToken cancellationToken = default);
}
