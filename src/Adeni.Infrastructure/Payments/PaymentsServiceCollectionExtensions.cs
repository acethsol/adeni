namespace Adeni.Infrastructure.Payments;

using Adeni.Application.Payments;
using Microsoft.Extensions.DependencyInjection;

public static class PaymentsServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services)
    {
        services.AddScoped<IPaymentProvider, StubPaymentProvider>();
        return services;
    }
}
