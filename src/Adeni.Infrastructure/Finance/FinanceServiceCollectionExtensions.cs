namespace Adeni.Infrastructure.Finance;

using Adeni.Application.Events;
using Adeni.Application.Finance;
using Adeni.Domain.Finance.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public static class FinanceServiceCollectionExtensions
{
    public static IServiceCollection AddFinanceModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FinanceOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.GetSection(PaymentsOptions.SectionName).Bind(options);
                config.GetSection(FinanceOptions.SectionName).Bind(options);
            });
        services.Configure<PaystackOptions>(configuration.GetSection(PaystackOptions.SectionName));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));

        services.AddHttpClient<PaystackPaymentProvider>();
        services.AddHttpClient<StripePaymentProvider>();
        services.AddScoped<StubPaymentProvider>();

        var provider = configuration.GetSection(FinanceOptions.SectionName)["Provider"]
            ?? configuration.GetSection(PaymentsOptions.SectionName)["Provider"]
            ?? "Stub";

        if (string.Equals(provider, "Paystack", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<PaystackPaymentProvider>());
        }
        else if (string.Equals(provider, "Stripe", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<StripePaymentProvider>());
        }
        else if (string.Equals(provider, "MarketDefault", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IPaymentProvider, MarketDefaultPaymentProvider>();
        }
        else
        {
            services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<StubPaymentProvider>());
        }

        services.AddScoped<IPaymentProviderRegistry, PaymentProviderRegistry>();
        services.AddScoped<IStaffCompensationService, StaffCompensationService>();
        services.AddScoped<IPaymentAccessService, PaymentAccessService>();
        services.AddScoped<ICustomerCommerceQuery, CustomerCommerceQuery>();
        services.AddScoped<IPlatformFeeCalculator, PlatformFeeCalculator>();
        services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
        services.AddScoped<IBillingDocumentService, BillingDocumentService>();
        services.AddScoped<Adeni.Application.Reporting.IBillingDocumentPreviewSource, BillingDocumentService>();
        services.AddScoped<PaymentBookingHandler>();
        services.AddScoped<IDomainEventHandler<PaymentCompleted>, PaymentBookingHandler>();
        services.AddScoped<IDomainEventHandler<PaymentFailed>, PaymentBookingHandler>();
        services.AddScoped<IDomainEventHandler<RefundInitiated>, PaymentBookingHandler>();

        return services;
    }
}
