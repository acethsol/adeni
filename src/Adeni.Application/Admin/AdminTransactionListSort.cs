namespace Adeni.Application.Admin;

using Adeni.Application.Common;

public static class AdminTransactionListSort
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "when",
        "type",
        "business",
        "reference",
        "amount",
        "status",
    };

    public const string DefaultField = "when";

    public const ListSortDirection DefaultDirection = ListSortDirection.Desc;
}
