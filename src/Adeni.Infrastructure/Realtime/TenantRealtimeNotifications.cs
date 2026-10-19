namespace Adeni.Infrastructure.Realtime;

using Adeni.Application.Realtime;
using Adeni.Domain.Booking;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

internal static class TenantRealtimeNotifications
{
    internal static async Task PublishPendingBookingsAsync(
        ITenantRealtimePublisher publisher,
        AdeniDbContext dbContext,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var pendingCount = await dbContext.Bookings
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId && x.Status == BookingStatus.Pending, cancellationToken);
        await publisher.PublishPendingBookingsChangedAsync(tenantId, pendingCount, cancellationToken);
    }

    internal static async Task PublishQuotePipelineAsync(
        ITenantRealtimePublisher publisher,
        AdeniDbContext dbContext,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var openCount = await dbContext.QuoteRequests
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenantId
                    && (x.Status == QuoteRequestStatus.Submitted || x.Status == QuoteRequestStatus.Quoted),
                cancellationToken);
        await publisher.PublishQuotePipelineChangedAsync(tenantId, openCount, cancellationToken);
    }

    internal static Task PublishQueueFloorAsync(
        Guid tenantId,
        ITenantRealtimePublisher publisher,
        CancellationToken cancellationToken) =>
        publisher.PublishQueueFloorChangedAsync(tenantId, cancellationToken);

    internal static async Task PublishAdminVerificationAsync(
        ITenantRealtimePublisher publisher,
        AdeniDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var pendingCount = await dbContext.Tenants
            .AsNoTracking()
            .CountAsync(x => x.Status == TenantStatus.PendingVerification, cancellationToken);
        await publisher.PublishAdminVerificationChangedAsync(pendingCount, cancellationToken);
    }
}
