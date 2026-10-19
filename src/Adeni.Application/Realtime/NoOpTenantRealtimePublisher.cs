namespace Adeni.Application.Realtime;

public sealed class NoOpTenantRealtimePublisher : ITenantRealtimePublisher
{
    public Task PublishPendingBookingsChangedAsync(
        Guid tenantId,
        int pendingCount,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PublishQueueFloorChangedAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task PublishQuotePipelineChangedAsync(
        Guid tenantId,
        int openCount,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PublishAdminVerificationChangedAsync(
        int pendingCount,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PublishPaymentCompletedAsync(
        Guid tenantId,
        Guid paymentIntentId,
        Guid? bookingId,
        string? providerReference = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
