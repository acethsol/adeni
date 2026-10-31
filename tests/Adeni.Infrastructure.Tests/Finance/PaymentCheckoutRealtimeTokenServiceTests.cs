namespace Adeni.Infrastructure.Tests.Finance;

using Adeni.Infrastructure.Finance;
using Microsoft.Extensions.Options;

public sealed class PaymentCheckoutRealtimeTokenServiceTests
{
    private static PaymentCheckoutRealtimeTokenService CreateService() =>
        new(Options.Create(new FinanceOptions { CheckoutRealtimeSigningKey = "test-signing-key" }));

    [Fact]
    public void IssueAndResolve_RoundTripsProviderReference()
    {
        var service = CreateService();
        var intentId = Guid.NewGuid();
        var token = service.Issue(intentId, "pay_ref_123");

        Assert.True(service.TryResolve(token, out var claims));
        Assert.NotNull(claims);
        Assert.Equal(intentId, claims!.PaymentIntentId);
        Assert.Equal("pay_ref_123", claims.ProviderReference);
    }

    [Fact]
    public void TryResolve_RejectsTamperedToken()
    {
        var service = CreateService();
        var token = service.Issue(Guid.NewGuid(), "pay_ref_123");
        var tampered = token[..^4] + "ffff";

        Assert.False(service.TryResolve(tampered, out _));
    }
}
