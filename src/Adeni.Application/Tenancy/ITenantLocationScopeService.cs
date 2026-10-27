namespace Adeni.Application.Tenancy;

using Adeni.Domain.Common;

public interface ITenantLocationScopeService
{
    Task<Result<Guid?>> ResolveListLocationAsync(Guid tenantId, Guid? locationId, CancellationToken cancellationToken = default);
    Task<Result<Guid?>> ValidateOptionalLocationIdAsync(Guid tenantId, Guid? locationId, CancellationToken cancellationToken = default);
    Task<Result<Guid>> ResolveAvailabilityLocationAsync(Guid tenantId, Guid? locationId, CancellationToken cancellationToken = default);
}
