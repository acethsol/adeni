namespace Adeni.Infrastructure.Auth;

using System.Security.Claims;
using Adeni.Application.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public static class AuthServiceCollectionExtensions
{
    public const string AdminPolicy = "Admin";
    public const string BusinessPolicy = "Business";
    public const string CustomerPolicy = "Customer";
    public const string AdminMfaPolicy = "AdminMfa";

    public static IServiceCollection AddAdeniAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<Auth0Options>()
            .Bind(configuration.GetSection(Auth0Options.SectionName));

        var auth0 = configuration.GetSection(Auth0Options.SectionName).Get<Auth0Options>() ?? new Auth0Options();

        var authBuilder = services.AddAuthentication();

        if (auth0.Enabled && !string.IsNullOrWhiteSpace(auth0.Domain) && !string.IsNullOrWhiteSpace(auth0.Audience))
        {
            authBuilder.AddJwtBearer(options =>
            {
                options.Authority = $"https://{auth0.Domain.TrimEnd('/')}/";
                options.Audience = auth0.Audience;
                options.TokenValidationParameters = new()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = AdeniClaimTypes.Roles
                };
            });
        }

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy =>
                policy.RequireRole(AdeniRoles.Admin))
            .AddPolicy(BusinessPolicy, policy =>
                policy.RequireRole(AdeniRoles.Business))
            .AddPolicy(CustomerPolicy, policy =>
                policy.RequireRole(AdeniRoles.Customer))
            .AddPolicy(AdminMfaPolicy, policy =>
            {
                policy.RequireRole(AdeniRoles.Admin);
                policy.Requirements.Add(new AdminMfaRequirement());
            });

        services.AddSingleton<IAuthorizationHandler, AdminMfaAuthorizationHandler>();

        return services;
    }
}

public sealed class AdminMfaRequirement : IAuthorizationRequirement;

public sealed class AdminMfaAuthorizationHandler(IOptions<Auth0Options> options)
    : AuthorizationHandler<AdminMfaRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminMfaRequirement requirement)
    {
        if (!options.Value.RequireMfaForAdmin)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var amrValues = context.User.FindAll(AdeniClaimTypes.Amr).Select(c => c.Value).ToArray();
        if (amrValues.Any(v => v.Equals("mfa", StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
