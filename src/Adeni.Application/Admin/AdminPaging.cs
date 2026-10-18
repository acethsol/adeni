namespace Adeni.Application.Admin;

using Adeni.Application.Common;
using Adeni.Domain.Common;

public static class AdminPaging
{
    public const int DefaultPageSize = 10;

    public static Result<(int Page, int PageSize)> Normalize(int page, int pageSize)
    {
        if (page < 1)
        {
            return Result.Failure<(int Page, int PageSize)>(Error.Validation("Page must be at least 1."));
        }

        var effectivePageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, Paging.MaxPageSize);
        return Result.Success((page, effectivePageSize));
    }
}

public sealed record AdminPagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminStatusCount(int Status, int Count);

public sealed record AdminSkuCount(string Sku, int Count);

public sealed record AdminBusinessDirectoryStats(
    int TotalCount,
    int VerifiedCount,
    IReadOnlyList<AdminStatusCount> StatusMix,
    IReadOnlyList<AdminSkuCount> SkuMix);
