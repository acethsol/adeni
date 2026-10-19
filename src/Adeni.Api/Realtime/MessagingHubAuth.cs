namespace Adeni.Api.Realtime;

using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Microsoft.AspNetCore.SignalR;

internal static class MessagingHubAuth
{
    internal static string? ResolveAuth0Sub(HubCallerContext context, Auth0Options auth0Options)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            return context.User.FindFirst("sub")?.Value;
        }

        var http = context.GetHttpContext();
        if (http is null || auth0Options.Enabled)
        {
            return null;
        }

        if (http.Request.Headers.TryGetValue(DevBusinessAuthMiddleware.DevAuth0SubHeader, out var businessSub)
            && !string.IsNullOrWhiteSpace(businessSub))
        {
            return businessSub.ToString();
        }

        if (http.Request.Query.TryGetValue(DevBusinessAuthMiddleware.DevAuth0SubQuery, out var querySub)
            && !string.IsNullOrWhiteSpace(querySub))
        {
            return querySub.ToString();
        }

        if (http.Request.Headers.TryGetValue(DevCustomerAuthMiddleware.DevAuth0SubHeader, out var customerSub)
            && !string.IsNullOrWhiteSpace(customerSub))
        {
            return customerSub.ToString();
        }

        return null;
    }
}
