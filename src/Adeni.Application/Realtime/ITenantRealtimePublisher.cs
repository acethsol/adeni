namespace Adeni.Application.Realtime;

/// <summary>Push-only tenant/admin operational events after durable commits.</summary>
public interface ITenantRealtimePublisher
{
    Task PublishPendingBookingsChangedAsync(
        Guid tenantId,
        int pendingCount,
        CancellationToken cancellationToken = default);

    Task PublishQueueFloorChangedAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task PublishQuotePipelineChangedAsync(
        Guid tenantId,
        int openCount,
        CancellationToken cancellationToken = default);

    Task PublishAdminVerificationChangedAsync(
        int pendingCount,
        CancellationToken cancellationToken = default);

    Task PublishPaymentCompletedAsync(
        Guid tenantId,
        Guid paymentIntentId,
        Guid? bookingId,
        string? providerReference = null,
        CancellationToken cancellationToken = default);
}
