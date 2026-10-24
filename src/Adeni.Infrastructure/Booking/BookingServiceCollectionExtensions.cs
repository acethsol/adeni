namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Finance;
using Microsoft.Extensions.DependencyInjection;

public static class BookingServiceCollectionExtensions
{
    public static IServiceCollection AddBookingModule(this IServiceCollection services)
    {
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IServiceMenuGroupService, ServiceMenuGroupService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<ITenantSchedulingTimeZone, TenantSchedulingTimeZone>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<ICustomerPaymentAccessQuery, CustomerPaymentAccessQuery>();
        services.AddScoped<IWaitlistService, WaitlistService>();
        services.AddScoped<IQueueService, QueueService>();
        services.AddScoped<IKioskService, KioskService>();
        services.AddScoped<IBusinessClosedDateService, BusinessClosedDateService>();
        services.AddScoped<IQuoteRequestService, QuoteRequestService>();
        return services;
    }
}
