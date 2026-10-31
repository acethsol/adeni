namespace Adeni.Application.Identity;

public sealed class CustomerVerificationOptions
{
    public const string SectionName = "CustomerVerification";

    /// <summary>HMAC key for phone fingerprints and guest queue proof tokens (Key Vault in staging/prod).</summary>
    public string PhoneVerificationHmacKey { get; set; } = string.Empty;

    /// <summary>When true (default in Testing), dev customers pass email/phone gates without Auth0 claims.</summary>
    public bool AutoVerifyCustomerInTesting { get; set; } = true;

    /// <summary>When Required, public queue join must include valid phone verification proof.</summary>
    public GuestQueuePhoneProofMode GuestQueuePhoneProof { get; set; } = GuestQueuePhoneProofMode.RelaxedInDevelopment;
}

public enum GuestQueuePhoneProofMode
{
    RelaxedInDevelopment,
    Required,
}
