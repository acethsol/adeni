namespace Adeni.Api.Auth;

using Adeni.Api.Errors;
using Adeni.Application.Identity;
using Adeni.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

/// <summary>
/// When tenant on-premises policy is enabled, requires a valid verified presence session.
/// Must run after <see cref="RequiresBusinessPermissionAttribute"/> (same action/class).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiresOnPremisesPresenceAttribute : Attribute, IAsyncActionFilter
{
    public string LocationQueryParameter { get; init; } = "locationId";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var access = PortalAccessHttpContext.Get(context.HttpContext);
        if (access is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        Guid? locationId = null;
        if (!string.IsNullOrWhiteSpace(LocationQueryParameter)
            && context.HttpContext.Request.Query.TryGetValue(LocationQueryParameter, out var raw)
            && Guid.TryParse(raw, out var parsed))
        {
            locationId = parsed;
        }

        var policy = context.HttpContext.RequestServices.GetRequiredService<IOnPremisesSensitiveAccessPolicy>();
        var result = await policy.EnsureSensitiveAccessAsync(
            access.TenantId,
            access,
            locationId,
            context.HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            context.Result = ApiErrorResponseMapper.ToActionResult(result.Error, context.HttpContext);
            return;
        }

        await next();
    }
}
