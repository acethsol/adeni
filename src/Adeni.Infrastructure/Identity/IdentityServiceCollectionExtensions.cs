namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Auth;
using Adeni.Application.Identity;
using Adeni.Application.Intake;
using Microsoft.Extensions.DependencyInjection;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<IAuthSyncService, AuthSyncService>();
        services.AddScoped<ICustomerPhoneFingerprintService, CustomerPhoneFingerprintService>();
        services.AddScoped<ICustomerTrustQuery, CustomerTrustQuery>();
        services.AddScoped<ICustomerTrustSyncService, CustomerTrustSyncService>();
        services.AddScoped<ICustomerVerificationPolicy, CustomerVerificationPolicy>();
        services.AddSingleton<IGuestPhoneVerificationProofValidator, GuestPhoneVerificationProofValidator>();
        services.AddScoped<Adeni.Application.Tenancy.IBookingLinkAccessQuery, BookingLinkAccessQuery>();
        services.AddScoped<Adeni.Application.Calendar.ICalendarAccessQuery, CalendarAccessQuery>();
        services.AddScoped<Adeni.Application.Finance.IFinanceCommerceAccessQuery, FinanceCommerceAccessQuery>();
        services.AddScoped<IIntakeManagementAccessQuery, IntakeManagementAccessQuery>();
        services.AddScoped<IBusinessMembershipQuery, BusinessMembershipQuery>();
        services.AddScoped<IBusinessPermissionService, BusinessPermissionService>();
        services.AddScoped<IPortalTenantAccessQuery, PortalTenantAccessQuery>();
        services.AddScoped<ICustomerErasureQuery, PortalTenantAccessQuery>();
        services.AddScoped<IStaffAccessService, StaffAccessService>();
        services.AddScoped<IVerifiedPresenceService, VerifiedPresenceService>();
        services.AddScoped<IOnPremisesSensitiveAccessPolicy, OnPremisesSensitiveAccessPolicy>();
        return services;
    }
}
