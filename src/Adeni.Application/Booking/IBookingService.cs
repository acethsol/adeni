namespace Adeni.Application.Booking;

using Adeni.Domain.Booking;
using Adeni.Domain.Common;

public sealed record BookingResponse(
    Guid Id,
    Guid TenantId,
    Guid ServiceOfferingId,
    string ServiceName,
    Guid CustomerId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    BookingStatus Status,
    string? CustomerNotes,
    DateTimeOffset CreatedAt);

public sealed record CustomerBookingResponse(
    Guid Id,
    Guid TenantId,
    string BusinessName,
    string BusinessSlug,
    Guid ServiceOfferingId,
    string ServiceName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    BookingStatus Status,
    string? CustomerNotes,
    DateTimeOffset CreatedAt);

public sealed record CreateBookingRequest(
    Guid TenantId,
    Guid ServiceOfferingId,
    DateTimeOffset StartAt,
    string? CustomerNotes);

public interface IBookingService
{
    Task<Result<BookingResponse>> CreateAsync(
        string customerAuth0Sub,
        CreateBookingRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookingResponse>> ListForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerBookingResponse>> ListForCustomerAsync(
        string customerAuth0Sub,
        CancellationToken cancellationToken = default);

    Task<Result<BookingResponse>> AcceptAsync(
        Guid tenantId,
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task<Result<BookingResponse>> RejectAsync(
        Guid tenantId,
        Guid bookingId,
        string? reason,
        CancellationToken cancellationToken = default);
}
