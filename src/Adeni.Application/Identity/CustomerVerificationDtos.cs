namespace Adeni.Application.Identity;

public sealed record CustomerVerificationResponse(
    bool EmailVerified,
    bool PhoneVerified,
    DateTimeOffset? EmailVerifiedAt,
    DateTimeOffset? PhoneVerifiedAt);

public sealed record CustomerProfileTrustIndicators(bool PhoneVerified, DateTimeOffset CustomerSince);
