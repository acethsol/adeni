namespace Adeni.Api.Realtime;

using Adeni.Application.Realtime;
using Microsoft.AspNetCore.SignalR;

public sealed class SignalRTenantRealtimePublisher(IHubContext<OperationsHub> hub) : ITenantRealtimePublisher
{
    public Task PublishPendingBookingsChangedAsync(
        Guid tenantId,
        int pendingCount,
        CancellationToken cancellationToken = default) =>
        hub.Clients
            .Group(OperationsHub.TenantGroup(tenantId))
            .SendAsync("PendingBookingsChanged", new { tenantId, pendingCount }, cancellationToken);

    public Task PublishQueueFloorChangedAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        hub.Clients
            .Group(OperationsHub.TenantGroup(tenantId))
            .SendAsync("QueueFloorChanged", new { tenantId }, cancellationToken);

    public Task PublishQuotePipelineChangedAsync(
        Guid tenantId,
        int openCount,
        CancellationToken cancellationToken = default) =>
        hub.Clients
            .Group(OperationsHub.TenantGroup(tenantId))
            .SendAsync("QuotePipelineChanged", new { tenantId, openCount }, cancellationToken);

    public Task PublishAdminVerificationChangedAsync(
        int pendingCount,
        CancellationToken cancellationToken = default) =>
        hub.Clients
            .Group(OperationsHub.AdminVerificationGroup)
            .SendAsync("AdminVerificationChanged", new { pendingCount }, cancellationToken);

    public async Task PublishPaymentCompletedAsync(
        Guid tenantId,
        Guid paymentIntentId,
        Guid? bookingId,
        string? providerReference = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new { tenantId, paymentIntentId, bookingId, providerReference };
        await hub.Clients
            .Group(OperationsHub.TenantGroup(tenantId))
            .SendAsync("PaymentCompleted", payload, cancellationToken);

        if (!string.IsNullOrWhiteSpace(providerReference))
        {
            await hub.Clients
                .Group(OperationsHub.PaymentReferenceGroup(providerReference))
                .SendAsync("PaymentCompleted", payload, cancellationToken);
        }
    }
}
