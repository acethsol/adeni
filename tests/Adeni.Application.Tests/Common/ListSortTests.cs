namespace Adeni.Application.Tests.Common;

using Adeni.Application.Common;
using Adeni.Application.Tenancy;

public sealed class ListSortTests
{
    [Fact]
    public void Resolve_rejects_unknown_sort_field()
    {
        var result = ListSort.Resolve(
            "unknown-column",
            "asc",
            TenantCustomerListSort.Allowed,
            TenantCustomerListSort.DefaultField,
            TenantCustomerListSort.DefaultDirection);

        Assert.True(result.IsFailure);
        Assert.Equal("list.sort_invalid", result.Error!.Code);
    }
}
