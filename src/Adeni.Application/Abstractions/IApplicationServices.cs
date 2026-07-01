namespace Adeni.Application.Abstractions;

using Adeni.Domain.Auditing;
using Adeni.Domain.Tenancy;

public interface ICorrelationContext
{
    string CorrelationId { get; }
    void Set(string correlationId);
}

public interface ITenantContext
{
    TenantId? CurrentTenantId { get; }
    bool IsTenantFilterActive { get; }
    void EnableTenantFilter(TenantId tenantId);
    void DisableTenantFilter();
    void Set(TenantId tenantId);
    void Clear();
}

public interface IAuditLogWriter
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    string? UserId { get; }
    IReadOnlyCollection<string> Roles { get; }
    TenantId? TenantId { get; }
    bool HasMfa { get; }
}
