namespace Adeni.Application.Reviews;

using Adeni.Application.Admin;
using Adeni.Domain.Common;

public sealed record CreateReviewRequest(byte Rating, string? Comment);

public sealed record ReviewResponse(
    Guid Id,
    Guid BookingId,
    Guid TenantId,
    byte Rating,
    string Comment,
    DateTimeOffset CreatedAt);

public sealed record PublicReviewItem(
    Guid Id,
    byte Rating,
    string Comment,
    DateTimeOffset CreatedAt,
    string CustomerDisplayName);

public sealed record PublicReviewsResult(
    IReadOnlyList<PublicReviewItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record TenantRatingSummary(
    double? RatingAvg,
    int ReviewCount);

public interface IReviewService
{
    Task<Result<ReviewResponse>> CreateForBookingAsync(
        string customerAuth0Sub,
        Guid bookingId,
        CreateReviewRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PublicReviewsResult>> ListPublicBySlugAsync(
        string slug,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> HideAsync(
        Guid reviewId,
        string adminId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, TenantRatingSummary>> GetRatingSummariesAsync(
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, ReviewResponse>> GetReviewsForBookingsAsync(
        IReadOnlyCollection<Guid> bookingIds,
        CancellationToken cancellationToken = default);
}
