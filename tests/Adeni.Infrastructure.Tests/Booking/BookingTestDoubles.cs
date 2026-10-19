namespace Adeni.Infrastructure.Tests.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Realtime;
using Adeni.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

internal static class BookingTestServiceCollectionExtensions
{
    internal static IServiceCollection AddBookingTestRealtime(this IServiceCollection services)
    {
        services.AddSingleton<ITenantRealtimePublisher, NoOpTenantRealtimePublisher>();
        return services;
    }
}

internal sealed class NoOpQueueService : IQueueService
{
    public Task<Result<QueueTicketResponse>> JoinBySlugAsync(
        string tenantSlug,
        JoinQueueRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<QueueTicketResponse>> GetByPublicTokenAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<IReadOnlyList<QueueTicketResponse>>> ListForTenantAsync(
        Guid tenantId,
        Guid? requireStaffMemberId = null,
        bool includeFinishedToday = true,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<TenantQueueFloorResponse>> GetFloorForTenantAsync(
        Guid tenantId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result> UpsertTicketFromBookingCheckInAsync(
        Guid tenantId,
        Guid bookingId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());

    public Task<Result<WalkInSettingsResponse>> UpdateWalkInSettingsAsync(
        Guid tenantId,
        UpdateWalkInSettingsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<WalkInSettingsResponse>> OpenWalkInBlockAsync(
        Guid tenantId,
        OpenWalkInBlockRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<QueueTicketResponse>> AddForTenantAsync(
        Guid tenantId,
        StaffAddQueueRequest request,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<QueueTicketResponse>> CallAsync(
        Guid tenantId,
        Guid ticketId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<QueueTicketResponse>> StartAsync(
        Guid tenantId,
        Guid ticketId,
        StartQueueTicketRequest? request = null,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<QueueTicketResponse>> CompleteAsync(
        Guid tenantId,
        Guid ticketId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<QueueTicketResponse>> MarkNoShowAsync(
        Guid tenantId,
        Guid ticketId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<QueueTicketResponse>> CancelAsync(
        Guid tenantId,
        Guid ticketId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
