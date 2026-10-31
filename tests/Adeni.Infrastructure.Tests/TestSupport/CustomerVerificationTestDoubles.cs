namespace Adeni.Infrastructure.Tests.TestSupport;

using Adeni.Application.Identity;
using Adeni.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;

internal static class CustomerVerificationTestDoubles
{
    internal static void Register(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        services.Configure<CustomerVerificationOptions>(options =>
        {
            options.AutoVerifyCustomerInTesting = true;
            options.PhoneVerificationHmacKey = "infrastructure-test-hmac-key";
            options.GuestQueuePhoneProof = GuestQueuePhoneProofMode.RelaxedInDevelopment;
        });
        services.AddScoped<ICustomerPhoneFingerprintService, CustomerPhoneFingerprintService>();
        services.AddScoped<ICustomerTrustQuery, CustomerTrustQuery>();
        services.AddScoped<ICustomerTrustSyncService, CustomerTrustSyncService>();
        services.AddScoped<ICustomerVerificationPolicy, CustomerVerificationPolicy>();
        services.AddSingleton<IGuestPhoneVerificationProofValidator, GuestPhoneVerificationProofValidator>();
    }
}
