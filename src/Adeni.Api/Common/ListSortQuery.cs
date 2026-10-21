namespace Adeni.Api.Common;

using Adeni.Application.Common;
using Adeni.Domain.Common;

public static class ListSortQuery
{
    public static Result<(string? SortBy, ListSortDirection? SortDirection)> ResolveOptional(
        string? sortBy,
        string? sortDir,
        IReadOnlySet<string> allowedFields,
        string defaultField,
        ListSortDirection defaultDirection)
    {
        if (string.IsNullOrWhiteSpace(sortBy) && string.IsNullOrWhiteSpace(sortDir))
        {
            return Result.Success<(string?, ListSortDirection?)>((null, null));
        }

        var resolved = ListSort.Resolve(sortBy, sortDir, allowedFields, defaultField, defaultDirection);
        return resolved.IsFailure
            ? Result.Failure<(string?, ListSortDirection?)>(resolved.Error)
            : Result.Success<(string?, ListSortDirection?)>((resolved.Value.Field, resolved.Value.Direction));
    }
}
