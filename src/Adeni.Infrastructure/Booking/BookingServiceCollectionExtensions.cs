namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Intake;
using Adeni.Application.Finance;
using Microsoft.Extensions.DependencyInjection;

public static class BookingServiceCollectionExtensions
{
    public static IServiceCollection AddBookingModule(this IServiceCollection services)
    {
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IServiceMenuGroupService, ServiceMenuGroupService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IStaffAttendanceService, StaffAttendanceService>();
        services.AddScoped<ITenantSchedulingTimeZone, TenantSchedulingTimeZone>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<Adeni.Application.Tenancy.IBookingPreselectionQuery, BookingPreselectionQuery>();
        services.AddScoped<Adeni.Application.Calendar.ICalendarBookingQuery, CalendarBookingQuery>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IIntakeBookingQuery, IntakeBookingQuery>();
        services.AddScoped<IIntakeServiceQuery, IntakeServiceQuery>();
        services.AddScoped<Adeni.Application.Retention.IRetentionServiceLookup, RetentionServiceLookup>();
        services.AddScoped<ICustomerPaymentAccessQuery, CustomerPaymentAccessQuery>();
        services.AddScoped<IBookingDepositCalculator, BookingDepositCalculator>();
        services.AddScoped<ICustomerBookingActivityQuery, CustomerBookingActivityQuery>();
        services.AddScoped<ICustomerSegmentFactsQuery, CustomerSegmentFactsQuery>();
        services.AddScoped<Adeni.Application.Reporting.IDocumentBookingScopeQuery, DocumentBookingScopeQuery>();
        services.AddScoped<ICustomerCommerceBookingQuery, CustomerCommerceBookingQuery>();
        services.AddScoped<IWaitlistService, WaitlistService>();
        services.AddScoped<IQueueService, QueueService>();
        services.AddScoped<IKioskService, KioskService>();
        services.AddScoped<IBusinessClosedDateService, BusinessClosedDateService>();
        services.AddScoped<IQuoteRequestService, QuoteRequestService>();
        return services;
    }
}
