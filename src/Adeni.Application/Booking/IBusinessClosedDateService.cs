namespace Adeni.Application.Booking;

using Adeni.Domain.Common;

public sealed record BusinessClosedDateResponse(
    Guid Id,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason,
    DateTimeOffset CreatedAt,
    IReadOnlyList<Guid>? ConflictingBookingIds = null);

public sealed record CreateBusinessClosedDateRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason = null);

public interface IBusinessClosedDateService
{
    Task<Result<IReadOnlyList<BusinessClosedDateResponse>>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result<BusinessClosedDateResponse>> CreateAsync(
        Guid tenantId,
        CreateBusinessClosedDateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> IsClosedAsync(
        Guid tenantId,
        DateOnly localDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(DateOnly StartDate, DateOnly EndDate)>> ListRangesAsync(
        Guid tenantId,
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default);
}
