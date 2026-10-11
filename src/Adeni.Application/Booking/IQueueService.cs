namespace Adeni.Application.Booking;

using Adeni.Domain.Common;

public sealed record QueueTicketResponse(
    Guid Id,
    Guid TenantId,
    string Status,
    int Position,
    int? EtaMinutes,
    string DisplayName,
    string Phone,
    string? ServiceName,
    Guid? ServiceOfferingId,
    string? StaffDisplayName,
    Guid? StaffMemberId,
    int PartySize,
    string? Notes,
    DateTimeOffset JoinedAt,
    string PublicToken,
    Guid? BookingId,
    string? BusinessName = null);

public sealed record JoinQueueRequest(
    string DisplayName,
    string Phone,
    Guid? ServiceOfferingId = null,
    int PartySize = 1,
    string? Notes = null);

public sealed record StaffAddQueueRequest(
    string DisplayName,
    string Phone,
    Guid? ServiceOfferingId = null,
    Guid? StaffMemberId = null,
    int PartySize = 1,
    string? Notes = null);

public sealed record StartQueueTicketRequest(Guid? StaffMemberId = null);

public interface IQueueService
{
    Task<Result<QueueTicketResponse>> JoinBySlugAsync(
        string tenantSlug,
        JoinQueueRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<QueueTicketResponse>> GetByPublicTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<QueueTicketResponse>>> ListForTenantAsync(
        Guid tenantId,
        Guid? requireStaffMemberId = null,
        bool includeFinishedToday = true,
        CancellationToken cancellationToken = default);

    Task<Result<QueueTicketResponse>> AddForTenantAsync(
        Guid tenantId,
        StaffAddQueueRequest request,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default);

    Task<Result<QueueTicketResponse>> CallAsync(
        Guid tenantId,
        Guid ticketId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default);

    Task<Result<QueueTicketResponse>> StartAsync(
        Guid tenantId,
        Guid ticketId,
        StartQueueTicketRequest? request = null,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default);

    Task<Result<QueueTicketResponse>> CompleteAsync(
        Guid tenantId,
        Guid ticketId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default);

    Task<Result<QueueTicketResponse>> MarkNoShowAsync(
        Guid tenantId,
        Guid ticketId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default);

    Task<Result<QueueTicketResponse>> CancelAsync(
        Guid tenantId,
        Guid ticketId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default);
}
