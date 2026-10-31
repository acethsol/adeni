namespace Adeni.Application.Identity;

using System.Security.Claims;

public interface ICustomerTrustSyncService
{
    /// <summary>Idempotent sync from trusted JWT claims (and optional Testing auto-verify).</summary>
    Task SyncAsync(string auth0Sub, ClaimsPrincipal? user, CancellationToken cancellationToken = default);
}
