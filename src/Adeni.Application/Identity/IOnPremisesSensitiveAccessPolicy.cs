namespace Adeni.Application.Identity;

using Adeni.Application.Auth;
using Adeni.Domain.Common;

public interface IOnPremisesSensitiveAccessPolicy
{
    Task<Result> EnsureSensitiveAccessAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        Guid? requiredLocationId,
        CancellationToken cancellationToken = default);

    bool IsSensitiveReportKey(string reportKey);
}
