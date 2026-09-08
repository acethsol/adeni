namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Events;
using Adeni.Application.Notifications;
using Adeni.Domain.Booking.Events;
using Microsoft.Extensions.DependencyInjection;

public static class NotificationsServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddScoped<LoggingNotificationDispatcher>();
        services.AddScoped<INotificationDispatcher, PreferenceAwareNotificationDispatcher>();
        services.AddScoped<INotificationPreferencesService, NotificationPreferencesService>();
        services.AddScoped<IDomainEventHandler<BookingConfirmed>, BookingNotificationHandler>();
        services.AddScoped<IDomainEventHandler<BookingRejected>, BookingNotificationHandler>();
        services.AddScoped<IDomainEventHandler<BookingCancelled>, BookingNotificationHandler>();
        services.AddScoped<IDomainEventHandler<SlotOpened>, WaitlistNotificationHandler>();
        return services;
    }
}
