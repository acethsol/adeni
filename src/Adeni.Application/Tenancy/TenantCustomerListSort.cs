namespace Adeni.Application.Tenancy;

using Adeni.Application.Common;

public static class TenantCustomerListSort
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "email",
        "account",
        "lastSeen",
    };

    public const string DefaultField = "lastSeen";

    public const ListSortDirection DefaultDirection = ListSortDirection.Desc;
}
