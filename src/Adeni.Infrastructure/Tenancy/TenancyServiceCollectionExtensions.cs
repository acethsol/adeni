namespace Adeni.Infrastructure.Tenancy;

using Adeni.Application.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class TenancyServiceCollectionExtensions
{
    public static IServiceCollection AddTenancyModule(this IServiceCollection services)
    {
        services.AddScoped<IBusinessOnboardingService, BusinessOnboardingService>();
        services.AddScoped<IBookingLinkService, BookingLinkService>();
        services.AddScoped<IBusinessLocationService, BusinessLocationService>();
        services.AddScoped<ITenantCustomerService, TenantCustomerService>();
        services.AddScoped<ITenantLocationScopeService, TenantLocationScopeService>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddDataProtection();
        services.AddScoped<ICustomerProfileContextQuery, CustomerProfileContextQuery>();
        services.AddScoped<ICustomerActivityCursorCodec, CustomerActivityCursorCodec>();
        services.AddScoped<ICustomerProfileQueryService, CustomerProfileQueryService>();
        services.AddScoped<ICustomerSegmentContextQuery, CustomerSegmentContextQuery>();
        services.AddScoped<ICustomerSegmentsQueryService, CustomerSegmentsQueryService>();
        return services;
    }
}
