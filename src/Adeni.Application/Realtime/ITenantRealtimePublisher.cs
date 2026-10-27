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

    /// <summary>Tenant group only: admin approved/rejected verification — portal reloads profile.</summary>
    Task PublishTenantVerificationChangedAsync(
        Guid tenantId,
        string status,
        CancellationToken cancellationToken = default);

    /// <summary>Tenant group only: trust badge requested/granted/revoked — portal reloads badge rows.</summary>
    Task PublishTenantBadgesChangedAsync(
        Guid tenantId,
        string? badgeType,
        string status,
        CancellationToken cancellationToken = default);

    /// <summary>Admin verification group only: pending trust-badge queue size changed.</summary>
    Task PublishAdminBadgesChangedAsync(
        int pendingCount,
        CancellationToken cancellationToken = default);

    Task PublishPaymentCompletedAsync(
        Guid tenantId,
        Guid paymentIntentId,
        Guid? bookingId,
        string? providerReference = null,
        CancellationToken cancellationToken = default);
}
