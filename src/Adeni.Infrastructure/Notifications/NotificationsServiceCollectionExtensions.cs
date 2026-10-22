namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Events;
using Adeni.Application.Notifications;
using Adeni.Domain.Booking.Events;
using Adeni.Domain.Finance.Events;
using Adeni.Domain.Messaging.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class NotificationsServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<NotificationsOptions>(configuration.GetSection(NotificationsOptions.SectionName));

        services.AddHttpClient<ResendEmailNotificationProvider>();
        services.AddHttpClient<TwilioSmsNotificationProvider>();
        services.AddScoped<LoggingEmailNotificationProvider>();
        services.AddScoped<LoggingSmsNotificationProvider>();
        services.AddScoped<ResendEmailNotificationProvider>();
        services.AddScoped<TwilioSmsNotificationProvider>();

        services.AddScoped<IEmailNotificationProvider>(sp => ResolveEmailProvider(sp));
        services.AddScoped<ISmsNotificationProvider>(sp => ResolveSmsProvider(sp));

        services.AddScoped<INotificationSenderConfigResolver, NotificationSenderConfigResolver>();
        services.AddScoped<INotificationRecipientResolver, NotificationRecipientResolver>();
        services.AddScoped<ChannelNotificationDispatcher>();
        services.AddScoped<IdempotentNotificationDispatcher>();
        services.AddScoped<EntitlementAwareNotificationDispatcher>();
        services.AddScoped<PreferenceAwareNotificationDispatcher>();
        services.AddScoped<INotificationDeliveryDispatcher>(sp =>
            sp.GetRequiredService<PreferenceAwareNotificationDispatcher>());
        services.AddScoped<DeferredNotificationDispatcher>();
        services.AddScoped<INotificationDispatcher>(sp =>
            sp.GetRequiredService<DeferredNotificationDispatcher>());
        services.AddScoped<INotificationPreferencesService, NotificationPreferencesService>();
        services.AddScoped<INotificationTemplateRenderer, NotificationTemplateRenderer>();
        services.AddScoped<NotificationTemplateDeliveryComposer>();
        services.AddScoped<INotificationLocaleResolver, NotificationLocaleResolver>();

        services.AddScoped<PaymentReceiptNotificationSender>();
        services.AddScoped<NotificationSendWorker>();
        services.AddScoped<NotificationSendHangfireJob>();

        services.AddScoped<IDomainEventHandler<BookingConfirmed>, BookingNotificationHandler>();
        services.AddScoped<IDomainEventHandler<BookingRejected>, BookingNotificationHandler>();
        services.AddScoped<IDomainEventHandler<BookingCancelled>, BookingNotificationHandler>();
        services.AddScoped<IDomainEventHandler<SlotOpened>, WaitlistNotificationHandler>();
        services.AddScoped<IDomainEventHandler<ConversationMessageCreated>, MessagingNotificationHandler>();
        services.AddScoped<IDomainEventHandler<PaymentCompleted>, FinanceNotificationHandler>();
        services.AddScoped<IDomainEventHandler<PaymentFailed>, FinanceNotificationHandler>();

        services.AddScoped<AppointmentReminderProcessor>();
        services.AddScoped<AppointmentReminderHangfireJob>();

        return services;
    }

    private static IEmailNotificationProvider ResolveEmailProvider(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotificationsOptions>>().Value;
        if (!string.IsNullOrWhiteSpace(options.Resend.ApiKey))
        {
            return sp.GetRequiredService<ResendEmailNotificationProvider>();
        }

        var env = sp.GetRequiredService<IHostEnvironment>();
        if (env.IsDevelopment() || env.EnvironmentName == "Testing")
        {
            return sp.GetRequiredService<LoggingEmailNotificationProvider>();
        }

        return sp.GetRequiredService<LoggingEmailNotificationProvider>();
    }

    private static ISmsNotificationProvider ResolveSmsProvider(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotificationsOptions>>().Value;
        var twilio = options.Twilio;
        if (!string.IsNullOrWhiteSpace(twilio.AccountSid) && !string.IsNullOrWhiteSpace(twilio.AuthToken))
        {
            return sp.GetRequiredService<TwilioSmsNotificationProvider>();
        }

        var env = sp.GetRequiredService<IHostEnvironment>();
        if (env.IsDevelopment() || env.EnvironmentName == "Testing")
        {
            return sp.GetRequiredService<LoggingSmsNotificationProvider>();
        }

        return sp.GetRequiredService<LoggingSmsNotificationProvider>();
    }
}
