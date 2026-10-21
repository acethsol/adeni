namespace Adeni.Application.Common;

using Adeni.Domain.Common;

public enum ListSortDirection
{
    Asc,
    Desc,
}

public static class ListSort
{
    public static Result<(string Field, ListSortDirection Direction)> Resolve(
        string? sortBy,
        string? sortDir,
        IReadOnlySet<string> allowedFields,
        string defaultField,
        ListSortDirection defaultDirection = ListSortDirection.Desc)
    {
        var field = string.IsNullOrWhiteSpace(sortBy)
            ? defaultField
            : sortBy.Trim();

        if (!allowedFields.Contains(field))
        {
            return Result.Failure<(string Field, ListSortDirection Direction)>(
                ErrorCodes.ListSortInvalidError(field));
        }

        if (string.IsNullOrWhiteSpace(sortDir))
        {
            return Result.Success((field, defaultDirection));
        }

        var dirRaw = sortDir.Trim();
        if (dirRaw.Equals("asc", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success((field, ListSortDirection.Asc));
        }

        if (dirRaw.Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success((field, ListSortDirection.Desc));
        }

        return Result.Failure<(string Field, ListSortDirection Direction)>(
            ErrorCodes.ListSortInvalidError(field));
    }

    public static bool IsDescending(ListSortDirection direction) =>
        direction == ListSortDirection.Desc;
}
