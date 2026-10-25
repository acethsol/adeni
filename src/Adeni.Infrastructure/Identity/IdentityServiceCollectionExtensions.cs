namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Auth;
using Adeni.Application.Intake;
using Microsoft.Extensions.DependencyInjection;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<IAuthSyncService, AuthSyncService>();
        services.AddScoped<Adeni.Application.Tenancy.IBookingLinkAccessQuery, BookingLinkAccessQuery>();
        services.AddScoped<Adeni.Application.Calendar.ICalendarAccessQuery, CalendarAccessQuery>();
        services.AddScoped<Adeni.Application.Finance.IFinanceCommerceAccessQuery, FinanceCommerceAccessQuery>();
        services.AddScoped<IIntakeManagementAccessQuery, IntakeManagementAccessQuery>();
        services.AddScoped<IBusinessMembershipQuery, BusinessMembershipQuery>();
        services.AddScoped<IPortalPermissionService, PortalPermissionService>();
        services.AddScoped<IPortalTenantAccessQuery, PortalTenantAccessQuery>();
        services.AddScoped<ICustomerErasureQuery, PortalTenantAccessQuery>();
        services.AddScoped<IStaffAccessService, StaffAccessService>();
        return services;
    }
}
