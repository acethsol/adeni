namespace Adeni.Infrastructure.Tests.Finance;

using Adeni.Application.Finance;
using Adeni.Infrastructure.Finance;
using Microsoft.Extensions.DependencyInjection;

internal static class FinanceTestRegistration
{
    internal static IServiceCollection AddCheckoutRealtimeTokens(this IServiceCollection services)
    {
        services.AddSingleton<IPaymentCheckoutRealtimeTokenService, PaymentCheckoutRealtimeTokenService>();
        return services;
    }
}
