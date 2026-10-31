namespace Adeni.Application.Identity;

using Adeni.Domain.Common;

public interface IGuestPhoneVerificationProofValidator
{
    Result Validate(string normalizedPhone, string? proof);
}
