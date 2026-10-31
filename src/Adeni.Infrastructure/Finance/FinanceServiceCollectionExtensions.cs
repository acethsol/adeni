namespace Adeni.Infrastructure.Finance;

using Adeni.Application.Events;
using Adeni.Application.Finance;
using Adeni.Domain.Finance.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

public static class FinanceServiceCollectionExtensions
{
    public static IServiceCollection AddFinanceModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<FinanceOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.GetSection(PaymentsOptions.SectionName).Bind(options);
                config.GetSection(FinanceOptions.SectionName).Bind(options);
                var publicWeb = config["App:PublicWebBaseUrl"]?.Trim();
                if (!string.IsNullOrWhiteSpace(publicWeb)
                    && Uri.TryCreate(publicWeb, UriKind.Absolute, out var publicUri))
                {
                    var origin = $"{publicUri.Scheme}://{publicUri.Authority}".TrimEnd('/');
                    if (!options.AllowedCallbackOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                    {
                        options.AllowedCallbackOrigins.Add(origin);
                    }
                }
            });
        services.AddScoped<IPaymentCallbackUrlValidator, PaymentCallbackUrlValidator>();
        services.Configure<PaystackOptions>(configuration.GetSection(PaystackOptions.SectionName));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));

        services.AddHttpClient<PaystackPaymentProvider>();
        services.AddHttpClient<StripePaymentProvider>();
        services.AddScoped<StubPaymentProvider>();
        services.AddSingleton<IPaymentCheckoutRealtimeTokenService, PaymentCheckoutRealtimeTokenService>();

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
        services.AddScoped<PaymentOrchestrator>();
        services.AddScoped<IPaymentOrchestrator>(sp => sp.GetRequiredService<PaymentOrchestrator>());
        services.AddScoped<ICommercePaymentLifecycle>(sp => sp.GetRequiredService<PaymentOrchestrator>());
        services.Configure<CommerceOptions>(configuration.GetSection(CommerceOptions.SectionName));
        services.AddScoped<CommerceCredentials>();
        services.AddScoped<CommerceReconciliationJob>();
        services.AddScoped<ICommerceService, CommerceService>();
        services.AddScoped<Adeni.Application.Tenancy.ICustomerGiftCardQuery, CustomerGiftCardQuery>();
        services.AddScoped<IGiftCardReportQuery, GiftCardReportQuery>();
        services.AddScoped<ICommercePaymentCompletion, CommercePaymentCompletion>();
        services.AddScoped<ICommercePrivacyService, CommercePrivacyService>();
        services.AddScoped<IBillingDocumentService, BillingDocumentService>();
        services.AddScoped<Adeni.Application.Reporting.IBillingDocumentPreviewSource, BillingDocumentService>();
        services.AddScoped<PaymentBookingHandler>();
        services.AddScoped<IDomainEventHandler<PaymentCompleted>, PaymentBookingHandler>();
        services.AddScoped<IDomainEventHandler<PaymentFailed>, PaymentBookingHandler>();
        services.AddScoped<IDomainEventHandler<RefundInitiated>, PaymentBookingHandler>();

        return services;
    }
}
