namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Microsoft.Extensions.DependencyInjection;

public static class BookingServiceCollectionExtensions
{
    public static IServiceCollection AddBookingModule(this IServiceCollection services)
    {
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<ITenantSchedulingTimeZone, TenantSchedulingTimeZone>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IWaitlistService, WaitlistService>();
        services.AddScoped<IQuoteRequestService, QuoteRequestService>();
        return services;
    }
}
