namespace Adeni.Api.Realtime;

using Adeni.Api.Messaging;
using Adeni.Application.Auth;
using Adeni.Application.Finance;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

/// <summary>Push-only hub for bookings, queue, quotes, payments, attendance, admin verification.</summary>
public sealed class OperationsHub(
    IOptions<Auth0Options> auth0Options,
    IBusinessPermissionService permissionService,
    IPaymentCheckoutRealtimeTokenService checkoutRealtimeTokens) : Hub
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

    /// <summary>Public checkout pages — signed token from payment initialize response.</summary>
    public async Task JoinPaymentCheckout(string checkoutRealtimeToken)
    {
        if (!checkoutRealtimeTokens.TryResolve(checkoutRealtimeToken, out var claims)
            || claims is null)
        {
            throw new HubException("Invalid checkout token");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            PaymentReferenceGroup(claims.ProviderReference));
    }

    public async Task LeavePaymentCheckout(string checkoutRealtimeToken)
    {
        if (!checkoutRealtimeTokens.TryResolve(checkoutRealtimeToken, out var claims)
            || claims is null)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            PaymentReferenceGroup(claims.ProviderReference));
    }
}
