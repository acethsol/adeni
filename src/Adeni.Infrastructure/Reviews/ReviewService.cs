namespace Adeni.Infrastructure.Reviews;

using Adeni.Application.Caching;
using Adeni.Application.Admin;
using Adeni.Application.Reviews;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class ReviewService(
    AdeniDbContext dbContext,
    ICacheService cache) : IReviewService
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 50;
    private const int MaxCommentLength = 1000;

    public async Task<Result<ReviewResponse>> CreateForBookingAsync(
        string customerAuth0Sub,
        Guid bookingId,
        CreateReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<ReviewResponse>(Error.Forbidden("Customer authentication is required."));
        }

        if (request.Rating is < 1 or > 5)
        {
            return Result.Failure<ReviewResponse>(Error.Validation("Rating must be between 1 and 5."));
        }

        var comment = request.Comment?.Trim() ?? string.Empty;
        if (comment.Length > MaxCommentLength)
        {
            return Result.Failure<ReviewResponse>(Error.Validation("Comment must be 1000 characters or fewer."));
        }

        var row = await (
            from booking in dbContext.Bookings
            join customer in dbContext.Customers on booking.CustomerId equals customer.Id
            where booking.Id == bookingId && customer.Auth0Sub == customerAuth0Sub
            select booking)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return Result.Failure<ReviewResponse>(Error.NotFound("Booking"));
        }

        if (row.Status != BookingStatus.Confirmed || row.EndAt > DateTimeOffset.UtcNow)
        {
            return Result.Failure<ReviewResponse>(
                Error.Validation("Only completed confirmed bookings can be reviewed."));
        }

        if (await dbContext.Reviews.AsNoTracking().AnyAsync(x => x.BookingId == bookingId, cancellationToken))
        {
            return Result.Failure<ReviewResponse>(Error.Conflict("This booking already has a review."));
        }

        var review = new Review
        {
            Id = Guid.NewGuid(),
            TenantId = row.TenantId,
            BookingId = row.Id,
            CustomerId = row.CustomerId,
            Rating = request.Rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateCachesForTenantAsync(row.TenantId, cancellationToken);

        return Result.Success(MapReview(review));
    }

    public async Task<Result<PublicReviewsResult>> ListPublicBySlugAsync(
        string slug,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<PublicReviewsResult>(Error.Validation("Business slug is required."));
        }

        if (page < 1)
        {
            return Result.Failure<PublicReviewsResult>(Error.Validation("Page must be at least 1."));
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var tenantId = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.IsActive && x.Slug == normalizedSlug)
            .Select(x => x.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantId == Guid.Empty)
        {
            return Result.Failure<PublicReviewsResult>(Error.NotFound("Business"));
        }

        var effectivePageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        var query = dbContext.Reviews
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsHidden)
            .Join(
                dbContext.Customers.AsNoTracking(),
                review => review.CustomerId,
                customer => customer.Id,
                (review, customer) => new { review, customer });

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.review.CreatedAt)
            .Skip((page - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(x => new PublicReviewItem(
                x.review.Id,
                x.review.Rating,
                x.review.Comment,
                x.review.CreatedAt,
                string.IsNullOrWhiteSpace(x.customer.Name) ? "Adeni customer" : x.customer.Name.Trim()))
            .ToListAsync(cancellationToken);

        return Result.Success(new PublicReviewsResult(items, page, effectivePageSize, totalCount));
    }

    public async Task<Result<Unit>> HideAsync(
        Guid reviewId,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var review = await dbContext.Reviews.FirstOrDefaultAsync(x => x.Id == reviewId, cancellationToken);
        if (review is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Review"));
        }

        if (review.IsHidden)
        {
            return Result.Success(Unit.Value);
        }

        review.IsHidden = true;
        review.HiddenAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateCachesForTenantAsync(review.TenantId, cancellationToken);

        return Result.Success(Unit.Value);
    }

    public async Task<IReadOnlyDictionary<Guid, TenantRatingSummary>> GetRatingSummariesAsync(
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken = default)
    {
        if (tenantIds.Count == 0)
        {
            return new Dictionary<Guid, TenantRatingSummary>();
        }

        var rows = await dbContext.Reviews
            .AsNoTracking()
            .Where(x => tenantIds.Contains(x.TenantId) && !x.IsHidden)
            .GroupBy(x => x.TenantId)
            .Select(g => new
            {
                TenantId = g.Key,
                RatingAvg = g.Average(x => (double)x.Rating),
                ReviewCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            x => x.TenantId,
            x => new TenantRatingSummary(Math.Round(x.RatingAvg, 1), x.ReviewCount));
    }

    public async Task<IReadOnlyDictionary<Guid, ReviewResponse>> GetReviewsForBookingsAsync(
        IReadOnlyCollection<Guid> bookingIds,
        CancellationToken cancellationToken = default)
    {
        if (bookingIds.Count == 0)
        {
            return new Dictionary<Guid, ReviewResponse>();
        }

        var reviews = await dbContext.Reviews
            .AsNoTracking()
            .Where(x => bookingIds.Contains(x.BookingId) && !x.IsHidden)
            .ToListAsync(cancellationToken);

        return reviews.ToDictionary(x => x.BookingId, MapReview);
    }

    private async Task InvalidateCachesForTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(CacheKeys.TenantProfile(tenantId), cancellationToken);

        var slugs = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => x.Slug)
            .ToListAsync(cancellationToken);

        foreach (var slug in slugs)
        {
            await cache.RemoveAsync(CacheKeys.LocationProfile(slug), cancellationToken);
        }
    }

    private static ReviewResponse MapReview(Review review) =>
        new(
            review.Id,
            review.BookingId,
            review.TenantId,
            review.Rating,
            review.Comment,
            review.CreatedAt);
}
