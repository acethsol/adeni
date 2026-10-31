namespace Adeni.Infrastructure.Tests.Identity;

using Adeni.Application.Identity;
using Adeni.Infrastructure.Identity;
using Microsoft.Extensions.Options;

public sealed class CustomerPhoneFingerprintServiceTests
{
    [Fact]
    public void Fingerprint_MatchesSamePhone_ChangesWhenPhoneChanges()
    {
        var service = CreateService("test-hmac-key");
        var ca = "+16135550100";
        var ng = "+2348012345678";

        var fpCa = service.ComputeFingerprint(ca);
        Assert.NotNull(fpCa);
        Assert.True(service.PhoneMatchesFingerprint(ca, fpCa));
        Assert.False(service.PhoneMatchesFingerprint(ng, fpCa));
    }

    [Fact]
    public void NormalizeCanadaAndNigeria_ProducesMatchingFingerprints()
    {
        var service = CreateService("another-key");
        Assert.True(service.PhoneMatchesFingerprint(
            "+16135550100",
            service.ComputeFingerprint("+16135550100")));
        Assert.True(service.PhoneMatchesFingerprint(
            "+2348012345678",
            service.ComputeFingerprint("+2348012345678")));
    }

    private static CustomerPhoneFingerprintService CreateService(string key) =>
        new(Options.Create(new CustomerVerificationOptions { PhoneVerificationHmacKey = key }));
}
