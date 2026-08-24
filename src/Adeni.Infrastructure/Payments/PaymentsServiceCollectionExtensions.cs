namespace Adeni.Infrastructure.Payments;

using Adeni.Application.Events;
using Adeni.Application.Payments;
using Adeni.Domain.Payments.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class PaymentsServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PaymentsOptions>(configuration.GetSection(PaymentsOptions.SectionName));
        services.Configure<PaystackOptions>(configuration.GetSection(PaystackOptions.SectionName));

        var provider = configuration.GetSection(PaymentsOptions.SectionName)["Provider"] ?? "Stub";

        if (string.Equals(provider, "Paystack", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IPaymentProvider, PaystackPaymentProvider>();
        }
        else
        {
            services.AddScoped<IPaymentProvider, StubPaymentProvider>();
        }

        services.AddScoped<IPlatformFeeCalculator, PlatformFeeCalculator>();
        services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
        services.AddScoped<PaymentBookingHandler>();
        services.AddScoped<PaymentNotificationHandler>();
        services.AddScoped<IDomainEventHandler<PaymentCompleted>, PaymentBookingHandler>();
        services.AddScoped<IDomainEventHandler<PaymentFailed>, PaymentBookingHandler>();
        services.AddScoped<IDomainEventHandler<RefundInitiated>, PaymentBookingHandler>();
        services.AddScoped<IDomainEventHandler<PaymentCompleted>, PaymentNotificationHandler>();
        services.AddScoped<IDomainEventHandler<PaymentFailed>, PaymentNotificationHandler>();

        return services;
    }
}
