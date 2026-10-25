namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Auth;
using Adeni.Application.Intake;
using Microsoft.Extensions.DependencyInjection;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<IAuthSyncService, AuthSyncService>();
        services.AddScoped<IIntakeManagementAccessQuery, IntakeManagementAccessQuery>();
        services.AddScoped<IBusinessMembershipQuery, BusinessMembershipQuery>();
        services.AddScoped<IPortalPermissionService, PortalPermissionService>();
        services.AddScoped<IStaffAccessService, StaffAccessService>();
        return services;
    }
}
