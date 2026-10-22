namespace Adeni.Infrastructure.Tests.Notifications;

using Adeni.Application.Notifications;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal static class NotificationJobTestServiceCollectionExtensions
{
    /// <summary>
    /// Registers after-commit <see cref="DeferredNotificationDispatcher"/> as <see cref="INotificationDispatcher"/>.
    /// Requires inner pipeline + <see cref="INotificationDeliveryDispatcher"/> already registered.
    /// </summary>
    internal static IServiceCollection AddDeferredNotificationDispatching(this IServiceCollection services)
    {
        if (!services.Any(sd => sd.ServiceType == typeof(IConfiguration)))
        {
            services.AddSingleton<IConfiguration>(_ => new ConfigurationBuilder().Build());
        }

        if (!services.Any(sd => sd.ServiceType == typeof(Application.Finance.IBillingDocumentService)))
        {
            services.AddSingleton<Application.Finance.IBillingDocumentService, NotificationDispatchTestBillingDocuments>();
            services.AddSingleton<Application.Reporting.IReportRenderer, NotificationDispatchTestReportRenderer>();
        }

        services.AddAdeniDomainEvents();
        services.AddScoped<PaymentReceiptNotificationSender>();
        services.AddScoped<NotificationSendWorker>();
        services.AddScoped<DeferredNotificationDispatcher>();
        services.AddScoped<INotificationDispatcher>(sp =>
            sp.GetRequiredService<DeferredNotificationDispatcher>());
        return services;
    }
}
