namespace Adeni.Application.Auth;

using Adeni.Domain.Common;

/// <summary>Tenant-specific access that rejects unrecognized persisted roles.</summary>
public interface IPortalTenantAccessQuery
{
    Task<Result<PortalAccessSnapshot>> ResolveAsync(Guid tenantId, string auth0Sub, CancellationToken ct);
}

public interface ICustomerErasureQuery
{
    Task<IReadOnlySet<Guid>> GetErasedAsync(IReadOnlyCollection<Guid> customerIds, CancellationToken ct);
}
