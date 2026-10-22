namespace Adeni.Infrastructure.Tests.Notifications;

using Adeni.Application.Notifications;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;

internal static class NotificationJobTestServiceCollectionExtensions
{
    internal static IServiceCollection AddInlineNotificationJobScheduling(this IServiceCollection services)
    {
        services.AddAdeniDomainEvents();
        services.AddScoped<PaymentReceiptNotificationSender>();
        services.AddScoped<NotificationSendWorker>();
        services.AddScoped<InlineNotificationJobScheduler>();
        services.AddScoped<INotificationJobScheduler, InlineNotificationJobScheduler>();
        return services;
    }
}
