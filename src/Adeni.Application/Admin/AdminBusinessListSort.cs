namespace Adeni.Application.Admin;

using Adeni.Application.Common;

public static class AdminBusinessListSort
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "business",
        "status",
        "plan",
        "billing",
    };

    public const string DefaultField = "business";

    public const ListSortDirection DefaultDirection = ListSortDirection.Desc;
}

public static class AdminPendingVerificationListSort
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "business",
        "market",
        "submitted",
        "docs",
    };

    public const string DefaultField = "submitted";

    public const ListSortDirection DefaultDirection = ListSortDirection.Asc;
}
