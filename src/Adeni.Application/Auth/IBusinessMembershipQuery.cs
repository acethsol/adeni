namespace Adeni.Application.Auth;

public interface IBusinessMembershipQuery
{
    Task<bool> IsMemberAsync(string auth0Sub, Guid tenantId, CancellationToken cancellationToken = default);
}
