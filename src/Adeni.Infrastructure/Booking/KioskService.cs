namespace Adeni.Infrastructure.Booking;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Adeni.Application.Booking;
using Adeni.Application.Catalog;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class KioskService(
    AdeniDbContext dbContext,
    IBusinessCapabilitiesService capabilities) : IKioskService
{
    private static readonly Regex DigitsOnly = new(@"^\d{4,8}$", RegexOptions.Compiled);

    public async Task<Result<KioskStatusResponse>> GetStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.BusinessProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<KioskStatusResponse>(Error.NotFound("Business profile"));
        }

        var queueEnabled = capabilities.HasCapability(
            profile.BusinessType,
            profile.CategorySlug,
            BusinessCapability.WalkInQueue);
        var slug = await ResolvePrimarySlugAsync(tenantId, cancellationToken);
        return Result.Success(new KioskStatusResponse(
            !string.IsNullOrEmpty(profile.KioskExitPinHash),
            slug,
            queueEnabled));
    }

    public async Task<Result<KioskStatusResponse>> SetPinAsync(
        Guid tenantId,
        SetKioskPinRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.BusinessProfiles
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<KioskStatusResponse>(Error.NotFound("Business profile"));
        }

        if (request.Pin is null)
        {
            profile.KioskExitPinHash = null;
        }
        else
        {
            var pin = request.Pin.Trim();
            if (!DigitsOnly.IsMatch(pin))
            {
                return Result.Failure<KioskStatusResponse>(
                    Error.Validation("PIN must be 4–8 digits."));
            }

            profile.KioskExitPinHash = HashPin(tenantId, pin);
        }

        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var slug = await ResolvePrimarySlugAsync(tenantId, cancellationToken);
        var queueEnabled = capabilities.HasCapability(
            profile.BusinessType,
            profile.CategorySlug,
            BusinessCapability.WalkInQueue);
        return Result.Success(new KioskStatusResponse(
            !string.IsNullOrEmpty(profile.KioskExitPinHash),
            slug,
            queueEnabled));
    }

    public async Task<Result<VerifyKioskPinResponse>> VerifyPinAsync(
        Guid tenantId,
        VerifyKioskPinRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.BusinessProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<VerifyKioskPinResponse>(Error.NotFound("Business profile"));
        }

        if (string.IsNullOrEmpty(profile.KioskExitPinHash))
        {
            return Result.Success(new VerifyKioskPinResponse(true));
        }

        var pin = request.Pin?.Trim() ?? string.Empty;
        if (!DigitsOnly.IsMatch(pin))
        {
            return Result.Success(new VerifyKioskPinResponse(false));
        }

        var hash = HashPin(tenantId, pin);
        var ok = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hash),
            Encoding.UTF8.GetBytes(profile.KioskExitPinHash));

        return Result.Success(new VerifyKioskPinResponse(ok));
    }

    internal static string HashPin(Guid tenantId, string pin) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId:N}:{pin}")))
            .ToLowerInvariant();

    private async Task<string?> ResolvePrimarySlugAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsPrimary)
            .Select(x => x.Slug)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
