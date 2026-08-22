namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Auth;
using Microsoft.Extensions.DependencyInjection;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<IAuthSyncService, AuthSyncService>();
        return services;
    }
}
