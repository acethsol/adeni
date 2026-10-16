namespace Adeni.Application.Common;

using Adeni.Domain.Common;

public static class Paging
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public static Result<(int Page, int PageSize)> Normalize(int page, int pageSize)
    {
        if (page < 1)
        {
            return Result.Failure<(int Page, int PageSize)>(Error.Validation("Page must be at least 1."));
        }

        var effectivePageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        return Result.Success((page, effectivePageSize));
    }
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
