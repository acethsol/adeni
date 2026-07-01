namespace Adeni.Domain.Tests.Tenancy;

using Adeni.Domain.Tenancy;

public sealed class TenantIdTests
{
    [Fact]
    public void Create_rejects_empty_guid()
    {
        var result = TenantId.Create(Guid.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    [Fact]
    public void Create_accepts_valid_guid()
    {
        var id = Guid.NewGuid();

        var result = TenantId.Create(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value!.Value);
    }

    [Fact]
    public void Empty_is_empty()
    {
        Assert.True(TenantId.Empty.IsEmpty);
    }

    [Fact]
    public void ToString_returns_guid_string()
    {
        var id = Guid.NewGuid();
        var tenantId = TenantId.Create(id).Value!.Value;

        Assert.Equal(id.ToString(), tenantId.ToString());
    }

    [Fact]
    public void Guid_extension_creates_tenant_id()
    {
        var id = Guid.NewGuid();

        var result = id.ToTenantId();

        Assert.True(result.IsSuccess);
    }
}

public sealed class AuditEntryTests
{
    [Fact]
    public void Audit_entry_record_holds_values()
    {
        var id = Guid.NewGuid();
        var occurred = DateTimeOffset.UtcNow;

        var entry = new Domain.Auditing.AuditEntry(
            id,
            "admin-1",
            "business.approved",
            "admin",
            "entity-1",
            "corr-1",
            occurred,
            "{}");

        Assert.Equal(id, entry.Id);
        Assert.Equal("admin-1", entry.ActorId);
        Assert.Equal("business.approved", entry.Action);
    }
}
