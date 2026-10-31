namespace Adeni.Infrastructure.Tests.Realtime;

using Adeni.Application.Realtime;

internal sealed class RecordingTenantRealtimePublisher : ITenantRealtimePublisher
{
    public List<int> AdminVerificationCounts { get; } = [];

    public List<int> AdminBadgeCounts { get; } = [];

    public List<(Guid TenantId, string Status)> TenantVerification { get; } = [];

    public List<(Guid TenantId, string? BadgeType, string Status)> TenantBadges { get; } = [];

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
        CancellationToken cancellationToken = default)
    {
        AdminVerificationCounts.Add(pendingCount);
        return Task.CompletedTask;
    }

    public Task PublishTenantVerificationChangedAsync(
        Guid tenantId,
        string status,
        CancellationToken cancellationToken = default)
    {
        TenantVerification.Add((tenantId, status));
        return Task.CompletedTask;
    }

    public Task PublishTenantBadgesChangedAsync(
        Guid tenantId,
        string? badgeType,
        string status,
        CancellationToken cancellationToken = default)
    {
        TenantBadges.Add((tenantId, badgeType, status));
        return Task.CompletedTask;
    }

    public Task PublishAdminBadgesChangedAsync(
        int pendingCount,
        CancellationToken cancellationToken = default)
    {
        AdminBadgeCounts.Add(pendingCount);
        return Task.CompletedTask;
    }

    public Task PublishPaymentCompletedAsync(
        Guid tenantId,
        Guid paymentIntentId,
        Guid? bookingId,
        string? providerReference = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PublishAttendanceTeamChangedAsync(
        Guid tenantId,
        Guid? businessLocationId = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
