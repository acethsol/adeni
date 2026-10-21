namespace Adeni.Application.Admin;

using Adeni.Application.Common;

public static class AdminMarketListSort
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "market",
        "country",
        "currency",
        "businesses",
        "status",
    };

    public const string DefaultField = "market";

    public const ListSortDirection DefaultDirection = ListSortDirection.Asc;
}
