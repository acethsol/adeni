namespace Adeni.Infrastructure.Context;

using Adeni.Application.Abstractions;
using Adeni.Domain.Tenancy;

public sealed class CorrelationContext : ICorrelationContext
{
    private string _correlationId = Guid.NewGuid().ToString("N");

    public string CorrelationId => _correlationId;

    public void Set(string correlationId) =>
        _correlationId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId;
}

public sealed class TenantContext : ITenantContext
{
    public TenantId? CurrentTenantId { get; private set; }

    public bool IsTenantFilterActive { get; private set; }

    public void EnableTenantFilter(TenantId tenantId)
    {
        CurrentTenantId = tenantId;
        IsTenantFilterActive = true;
    }

    public void DisableTenantFilter()
    {
        CurrentTenantId = null;
        IsTenantFilterActive = false;
    }

    public void Set(TenantId tenantId) => CurrentTenantId = tenantId;

    public void Clear()
    {
        CurrentTenantId = null;
        IsTenantFilterActive = false;
    }
}

public sealed class CurrentUser : ICurrentUser
{
    public string? UserId { get; init; }
    public IReadOnlyCollection<string> Roles { get; init; } = [];
    public TenantId? TenantId { get; init; }
    public bool HasMfa { get; init; }
}
