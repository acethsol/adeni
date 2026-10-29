namespace Adeni.Api.Messaging;

using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

/// <summary>Push-only messaging hub. Writes go through REST.</summary>
public sealed class MessagingHub(
    IOptions<Auth0Options> auth0Options,
    AdeniDbContext dbContext,
    IBusinessPermissionService permissionService,
    IEntitlementsService entitlementsService) : Hub
{
    public static string ConversationGroup(Guid conversationId) => $"conversation:{conversationId:D}";

    public static string TenantGroup(Guid tenantId) => $"tenant-messages:{tenantId:D}";

    public async Task JoinConversation(Guid conversationId)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null)
        {
            throw new HubException("Unauthorized");
        }

        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == conversationId);
        if (conversation is null)
        {
            throw new HubException("Conversation not found");
        }

        var isCustomer = await dbContext.Customers
            .AsNoTracking()
            .AnyAsync(x => x.Auth0Sub == auth0Sub && x.Id == conversation.CustomerId);
        if (isCustomer)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));
            return;
        }

        await EnsureBusinessMessagingAccessAsync(auth0Sub, conversation.TenantId);
        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));
    }

    public async Task LeaveConversation(Guid conversationId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ConversationGroup(conversationId));

    public async Task JoinTenantInbox(Guid tenantId)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null)
        {
            throw new HubException("Unauthorized");
        }

        await EnsureBusinessMessagingAccessAsync(auth0Sub, tenantId);
        await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup(tenantId));
    }

    private async Task EnsureBusinessMessagingAccessAsync(string auth0Sub, Guid tenantId)
    {
        var access = await permissionService.ResolveAsync(auth0Sub, Context.ConnectionAborted);
        if (access.IsFailure || access.Value is null || access.Value.TenantId != tenantId)
        {
            throw new HubException("Forbidden");
        }

        if (!BusinessPermissionRoles.HasAny(access.Value.PermissionRole, BusinessPermissions.Messages))
        {
            throw new HubException("Forbidden");
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is null)
        {
            throw new HubException("Forbidden");
        }

        var entitlement = await entitlementsService.EnsureCanUseMessagingForTenantAsync(
            tenant,
            Context.ConnectionAborted);
        if (entitlement.IsFailure)
        {
            throw new HubException("Forbidden");
        }
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
            && http.Request.Query.TryGetValue(DevBusinessAuthMiddleware.DevAuth0SubQuery, out var querySub)
            && !string.IsNullOrWhiteSpace(querySub))
        {
            return querySub.ToString();
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
