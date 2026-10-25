namespace Adeni.Infrastructure.Intake;

using Adeni.Application.Intake;
using Adeni.Application.Booking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class IntakeServiceCollectionExtensions
{
    public static IServiceCollection AddIntakeModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IIntakeTemplateService, IntakeTemplateService>();
        services.AddScoped<IIntakeSubmissionService, IntakeSubmissionService>();
        services.AddScoped<IIntakeServiceBinding, IntakeServiceBindingService>();
        services.AddScoped<IBookingIntakeAssignmentWriter, BookingIntakeAssignmentWriter>();
        services.AddScoped<IIntakeRetentionService, IntakeRetentionService>();
        services.AddScoped<IntakeRetentionHangfireJob>();
        return services;
    }
}
