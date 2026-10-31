namespace Adeni.Infrastructure.Tests.Identity;

using Adeni.Application.Identity;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class CustomerVerificationPolicyTests
{
    [Fact]
    public async Task RequireEmailVerified_FailsWhenNotVerifiedAndAutoVerifyOff()
    {
        await using var provider = BuildProvider(autoVerify: false);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        const string sub = "auth0|needs-email";
        db.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            Auth0Sub = sub,
            Name = "Test",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var policy = scope.ServiceProvider.GetRequiredService<ICustomerVerificationPolicy>();
        var result = await policy.RequireEmailVerifiedAsync(sub, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.CustomerEmailNotVerified, result.Error!.Code);
    }

    private static ServiceProvider BuildProvider(bool autoVerify)
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TenantContext>();
        services.AddScoped<Adeni.Application.Abstractions.ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.Configure<CustomerVerificationOptions>(o =>
        {
            o.AutoVerifyCustomerInTesting = autoVerify;
            o.PhoneVerificationHmacKey = "policy-test-key";
        });
        services.AddScoped<ICustomerPhoneFingerprintService, CustomerPhoneFingerprintService>();
        services.AddScoped<ICustomerTrustQuery, CustomerTrustQuery>();
        services.AddScoped<ICustomerVerificationPolicy, CustomerVerificationPolicy>();
        return services.BuildServiceProvider();
    }
}
