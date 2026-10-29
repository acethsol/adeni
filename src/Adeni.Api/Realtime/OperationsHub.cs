namespace Adeni.Api.Realtime;

using Adeni.Api.Messaging;
using Adeni.Application.Auth;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

/// <summary>Push-only hub for bookings, queue, quotes, payments, admin verification.</summary>
public sealed class OperationsHub(
    IOptions<Auth0Options> auth0Options,
    IBusinessPermissionService permissionService) : Hub
{
    public static string TenantGroup(Guid tenantId) => $"tenant-ops:{tenantId:D}";

    public static string AdminVerificationGroup => "admin-verification";

    public static string PaymentReferenceGroup(string providerReference) =>
        $"payment-ref:{providerReference.Trim()}";

    public async Task JoinTenant(Guid tenantId)
    {
        var auth0Sub = MessagingHubAuth.ResolveAuth0Sub(Context, auth0Options.Value);
        if (auth0Sub is null)
        {
            throw new HubException("Unauthorized");
        }

        var access = await permissionService.ResolveAsync(auth0Sub, Context.ConnectionAborted);
        if (access.IsFailure || access.Value is null || access.Value.TenantId != tenantId)
        {
            throw new HubException("Forbidden");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup(tenantId));
    }

    public async Task LeaveTenant(Guid tenantId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TenantGroup(tenantId));

    public async Task JoinAdminVerification()
    {
        var auth0Sub = MessagingHubAuth.ResolveAuth0Sub(Context, auth0Options.Value);
        if (auth0Sub is null)
        {
            throw new HubException("Unauthorized");
        }

        if (Context.User?.IsInRole(AdeniRoles.Admin) != true)
        {
            throw new HubException("Forbidden");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, AdminVerificationGroup);
    }

    /// <summary>Public checkout pages — reference acts as the capability token.</summary>
    public async Task JoinPaymentCheckout(string providerReference)
    {
        if (string.IsNullOrWhiteSpace(providerReference))
        {
            throw new HubException("Invalid reference");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            PaymentReferenceGroup(providerReference));
    }

    public async Task LeavePaymentCheckout(string providerReference)
    {
        if (string.IsNullOrWhiteSpace(providerReference))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            PaymentReferenceGroup(providerReference));
    }
}
