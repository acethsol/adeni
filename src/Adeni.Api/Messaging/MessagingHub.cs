namespace Adeni.Api.Messaging;

using System.Security.Claims;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

/// <summary>Push-only messaging hub. Writes go through REST.</summary>
public sealed class MessagingHub(IOptions<Auth0Options> auth0Options) : Hub
{
    public static string ConversationGroup(Guid conversationId) => $"conversation:{conversationId:D}";

    public static string TenantGroup(Guid tenantId) => $"tenant-messages:{tenantId:D}";

    public async Task JoinConversation(Guid conversationId)
    {
        if (ResolveAuth0Sub() is null)
        {
            throw new HubException("Unauthorized");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));
    }

    public async Task LeaveConversation(Guid conversationId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));

    public async Task JoinTenantInbox(Guid tenantId)
    {
        if (ResolveAuth0Sub() is null)
        {
            throw new HubException("Unauthorized");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup(tenantId));
    }

    private string? ResolveAuth0Sub()
    {
        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            return Context.User.FindFirst("sub")?.Value;
        }

        var http = Context.GetHttpContext();
        if (http is not null
            && !auth0Options.Value.Enabled
            && http.Request.Headers.TryGetValue(DevBusinessAuthMiddleware.DevAuth0SubHeader, out var businessSub)
            && !string.IsNullOrWhiteSpace(businessSub))
        {
            return businessSub.ToString();
        }

        if (http is not null
            && !auth0Options.Value.Enabled
            && http.Request.Headers.TryGetValue(DevCustomerAuthMiddleware.DevAuth0SubHeader, out var customerSub)
            && !string.IsNullOrWhiteSpace(customerSub))
        {
            return customerSub.ToString();
        }

        return null;
    }
}
