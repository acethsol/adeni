namespace Adeni.Api.Auth;

using System.Security.Claims;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

/// <summary>
/// Requires the current business user to hold at least one of the given portal RBAC permissions.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresPortalPermissionAttribute : Attribute, IAsyncActionFilter
{
    public RequiresPortalPermissionAttribute(string permission)
        : this([permission])
    {
    }

    public RequiresPortalPermissionAttribute(params string[] permissions)
    {
        Permissions = permissions is { Length: > 0 }
            ? permissions
            : [PortalPermissions.Overview];
    }

    public IReadOnlyList<string> Permissions { get; }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var auth0Options = context.HttpContext.RequestServices.GetRequiredService<IOptions<Auth0Options>>();
        var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPortalPermissionService>();

        var auth0Sub = ResolveAuth0Sub(context.HttpContext, auth0Options.Value.Enabled);
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var resolved = await permissionService.ResolveAsync(auth0Sub, context.HttpContext.RequestAborted);
        if (resolved.IsFailure || resolved.Value is null)
        {
            context.Result = ApiErrorResponseMapper.ToActionResult(resolved.Error, context.HttpContext);
            return;
        }

        if (!PortalPermissionRoles.HasAny(resolved.Value.PermissionRole, Permissions.ToArray()))
        {
            context.Result = ApiErrorResponseMapper.ToActionResult(
                ErrorCodes.PermissionDeniedError(),
                context.HttpContext);
            return;
        }

        PortalAccessHttpContext.Set(context.HttpContext, resolved.Value);
        await next();
    }

    private static string? ResolveAuth0Sub(HttpContext httpContext, bool auth0Enabled)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            return httpContext.User.FindFirstValue("sub");
        }

        if (!auth0Enabled
            && httpContext.Request.Headers.TryGetValue(DevBusinessAuthMiddleware.DevAuth0SubHeader, out var devSub)
            && !string.IsNullOrWhiteSpace(devSub))
        {
            return devSub.ToString();
        }

        return null;
    }
}
