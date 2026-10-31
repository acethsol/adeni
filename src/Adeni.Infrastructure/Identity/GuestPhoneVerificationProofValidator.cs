namespace Adeni.Infrastructure.Identity;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Adeni.Application.Identity;
using Adeni.Domain.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public sealed class GuestPhoneVerificationProofValidator(
    IOptions<CustomerVerificationOptions> options,
    IHostEnvironment environment,
    TimeProvider clock) : IGuestPhoneVerificationProofValidator
{
    private const string TestingProof = "testing-verified";

    public Result Validate(string normalizedPhone, string? proof)
    {
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return Result.Failure(ErrorCodes.QueuePhoneNotVerifiedError());
        }

        var mode = options.Value.GuestQueuePhoneProof;
        if (mode == GuestQueuePhoneProofMode.RelaxedInDevelopment
            && (environment.IsDevelopment() || environment.EnvironmentName == "Testing"))
        {
            if (string.IsNullOrWhiteSpace(proof) || proof == TestingProof)
            {
                return Result.Success();
            }
        }

        if (string.IsNullOrWhiteSpace(proof))
        {
            return Result.Failure(ErrorCodes.QueuePhoneNotVerifiedError());
        }

        if (environment.EnvironmentName == "Testing" && proof == TestingProof)
        {
            return Result.Success();
        }

        return ValidateHmacProof(normalizedPhone, proof);
    }

    private Result ValidateHmacProof(string normalizedPhone, string proof)
    {
        var key = options.Value.PhoneVerificationHmacKey?.Trim() ?? string.Empty;
        if (key.Length == 0)
        {
            return Result.Failure(ErrorCodes.QueuePhoneNotVerifiedError());
        }

        var parts = proof.Split('.', 2);
        if (parts.Length != 2)
        {
            return Result.Failure(ErrorCodes.QueuePhoneNotVerifiedError());
        }

        GuestProofPayload? payload;
        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            payload = JsonSerializer.Deserialize<GuestProofPayload>(json);
        }
        catch
        {
            return Result.Failure(ErrorCodes.QueuePhoneNotVerifiedError());
        }

        if (payload is null
            || !string.Equals(payload.Phone, normalizedPhone, StringComparison.Ordinal)
            || payload.Exp <= clock.GetUtcNow().ToUnixTimeSeconds())
        {
            return Result.Failure(ErrorCodes.QueuePhoneNotVerifiedError());
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var expected = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(parts[0])));
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(parts[1]);
        if (expectedBytes.Length != providedBytes.Length
            || !CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        {
            return Result.Failure(ErrorCodes.QueuePhoneNotVerifiedError());
        }

        return Result.Success();
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record GuestProofPayload(string Phone, long Exp);
}
