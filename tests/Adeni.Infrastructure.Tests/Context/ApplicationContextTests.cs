namespace Adeni.Infrastructure.Tests.Context;

using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;

public sealed class ApplicationContextTests
{
    [Fact]
    public void CorrelationContext_defaults_to_non_empty()
    {
        var context = new CorrelationContext();

        Assert.False(string.IsNullOrWhiteSpace(context.CorrelationId));
    }

    [Fact]
    public void CorrelationContext_set_replaces_value()
    {
        var context = new CorrelationContext();

        context.Set("abc");

        Assert.Equal("abc", context.CorrelationId);
    }

    [Fact]
    public void CorrelationContext_set_empty_generates_new_id()
    {
        var context = new CorrelationContext();
        var before = context.CorrelationId;

        context.Set("");

        Assert.NotEqual(before, context.CorrelationId);
        Assert.False(string.IsNullOrWhiteSpace(context.CorrelationId));
    }

    [Fact]
    public void TenantContext_enable_and_disable_filter()
    {
        var context = new TenantContext();
        var tenant = TenantId.Create(Guid.NewGuid()).Value!;

        context.EnableTenantFilter(tenant);

        Assert.Equal(tenant, context.CurrentTenantId);
        Assert.True(context.IsTenantFilterActive);

        context.DisableTenantFilter();

        Assert.Null(context.CurrentTenantId);
        Assert.False(context.IsTenantFilterActive);
    }

    [Fact]
    public void TenantContext_clear_resets_filter()
    {
        var context = new TenantContext();
        context.EnableTenantFilter(TenantId.Create(Guid.NewGuid()).Value!);

        context.Clear();

        Assert.False(context.IsTenantFilterActive);
    }

    [Fact]
    public void CurrentUser_holds_values()
    {
        var tenant = TenantId.Create(Guid.NewGuid()).Value!;
        var user = new CurrentUser
        {
            UserId = "user-1",
            Roles = ["admin"],
            TenantId = tenant,
            HasMfa = true
        };

        Assert.Equal("user-1", user.UserId);
        Assert.Contains("admin", user.Roles);
        Assert.Equal(tenant, user.TenantId);
        Assert.True(user.HasMfa);
    }
}
