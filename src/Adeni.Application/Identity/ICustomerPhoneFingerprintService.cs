namespace Adeni.Application.Identity;

public interface ICustomerPhoneFingerprintService
{
    string? ComputeFingerprint(string? normalizedE164Phone);

    bool PhoneMatchesFingerprint(string? normalizedE164Phone, string? storedFingerprint);
}
