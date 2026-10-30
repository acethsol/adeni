namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Retention;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class ReferralProgramService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    TimeProvider clock,
    IAuditLogWriter? audit = null,
    ICorrelationContext? correlation = null) : IReferralProgramService
{
    public async Task<Result<ReferralProgramResponse>> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var basic = await entitlements.EnsureLoyaltyBasicEntitledAsync(tenantId, cancellationToken);
        if (basic.IsFailure)
        {
            return Result.Failure<ReferralProgramResponse>(basic.Error);
        }

        if (!await entitlements.IsReferralsEntitledAsync(tenantId, cancellationToken))
        {
            return Result.Success(ReferralMapper.Defaults());
        }

        var program = await db.Set<ReferralProgram>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        return Result.Success(program is null ? ReferralMapper.Defaults() : ReferralMapper.ToResponse(program));
    }

    public async Task<Result<ReferralProgramResponse>> UpsertAsync(
        Guid tenantId,
        RetentionActor actor,
        UpsertReferralProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureReferralsEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<ReferralProgramResponse>(entitled.Error);
        }

        var basic = await entitlements.EnsureLoyaltyBasicEntitledAsync(tenantId, cancellationToken);
        if (basic.IsFailure)
        {
            return Result.Failure<ReferralProgramResponse>(basic.Error);
        }

        if (!actor.IsOwner)
        {
            return Result.Failure<ReferralProgramResponse>(RetentionErrorCodes.OwnerRequiredError());
        }

        var invalid = Validate(request);
        if (invalid is not null)
        {
            return Result.Failure<ReferralProgramResponse>(invalid);
        }

        var now = clock.GetUtcNow();
        var program = await db.Set<ReferralProgram>().FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (program is null)
        {
            program = new ReferralProgram { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = now };
            db.Add(program);
        }

        program.IsEnabled = request.IsEnabled;
        program.ReferrerRewardPoints = request.ReferrerRewardPoints;
        program.RefereeRewardPoints = request.RefereeRewardPoints;
        program.MaxRewardsPerReferrer = request.MaxRewardsPerReferrer;
        program.UpdatedBy = actor.Id;
        program.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync(actor.Id, program.Id, tenantId, cancellationToken);
        return Result.Success(ReferralMapper.ToResponse(program));
    }

    private static Error? Validate(UpsertReferralProgramRequest request)
    {
        if (request.ReferrerRewardPoints is < 0 or > ReferralProgram.MaxRewardPoints
            || request.RefereeRewardPoints is < 0 or > ReferralProgram.MaxRewardPoints)
        {
            return RetentionErrorCodes.ValidationError(
                $"Referral rewards must be between 0 and {ReferralProgram.MaxRewardPoints:N0} points.");
        }

        if (request is { IsEnabled: true, ReferrerRewardPoints: 0, RefereeRewardPoints: 0 })
        {
            return RetentionErrorCodes.ValidationError("Enable at least one side of the referral reward.");
        }

        if (request.MaxRewardsPerReferrer is < 0)
        {
            return RetentionErrorCodes.ValidationError("Max rewards per referrer cannot be negative.");
        }

        return null;
    }

    private Task AuditAsync(string actorId, Guid programId, Guid tenantId, CancellationToken cancellationToken) =>
        audit?.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actorId,
                AuditActions.RetentionReferralProgramSaved,
                "retention.referral_program",
                programId.ToString(),
                correlation?.CorrelationId ?? "background",
                clock.GetUtcNow(),
                $"{{\"tenantId\":\"{tenantId}\",\"programId\":\"{programId}\"}}"),
            cancellationToken) ?? Task.CompletedTask;
}

public sealed class ReferralCodeService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    TimeProvider clock) : IReferralCodeService
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<Result<ReferralCodeResponse>> GetOrCreateForCustomerAsync(
        Guid tenantId,
        Guid tenantCustomerId,
        CancellationToken cancellationToken = default)
    {
        var basic = await entitlements.EnsureLoyaltyBasicEntitledAsync(tenantId, cancellationToken);
        if (basic.IsFailure)
        {
            return Result.Failure<ReferralCodeResponse>(basic.Error);
        }

        if (!await entitlements.IsReferralsEntitledAsync(tenantId, cancellationToken))
        {
            return Result.Failure<ReferralCodeResponse>(RetentionErrorCodes.ReferralsNotEntitledError());
        }

        var exists = await db.TenantCustomers.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == tenantCustomerId, cancellationToken);
        if (!exists)
        {
            return Result.Failure<ReferralCodeResponse>(Error.NotFound("Customer"));
        }

        var program = await db.Set<ReferralProgram>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (program is null || !program.IsEnabled)
        {
            return Result.Failure<ReferralCodeResponse>(RetentionErrorCodes.ReferralProgramNotConfiguredError());
        }

        var row = await db.Set<ReferralCode>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId, cancellationToken);
        if (row is null)
        {
            row = await CreateUniqueCodeAsync(tenantId, tenantCustomerId, cancellationToken);
        }

        var slug = await db.Set<PublicBookingLink>().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Slug)
            .FirstOrDefaultAsync(cancellationToken);
        var sharePath = slug is null ? null : $"/businesses/{slug}?ref={Uri.EscapeDataString(row.Code)}";
        return Result.Success(new ReferralCodeResponse(tenantCustomerId, row.Code, sharePath, row.CreatedAt));
    }

    private async Task<ReferralCode> CreateUniqueCodeAsync(
        Guid tenantId, Guid tenantCustomerId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var code = GenerateCode();
            var exists = await db.Set<ReferralCode>().AsNoTracking()
                .AnyAsync(x => x.TenantId == tenantId && x.Code == code, cancellationToken);
            if (exists)
            {
                continue;
            }

            var now = clock.GetUtcNow();
            var row = new ReferralCode
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TenantCustomerId = tenantCustomerId,
                Code = code,
                CreatedAt = now,
            };
            db.Add(row);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return row;
            }
            catch (DbUpdateException) when (attempt < 11)
            {
                db.Entry(row).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException("Could not allocate a unique referral code.");
    }

    private static string GenerateCode()
    {
        Span<char> buffer = stackalloc char[8];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)];
        }

        return new string(buffer);
    }
}

internal static class ReferralMapper
{
    public static ReferralProgramResponse Defaults() =>
        new(false, null, false, 100, 50, null, null, null);

    public static ReferralProgramResponse ToResponse(ReferralProgram program) =>
        new(
            true,
            program.Id,
            program.IsEnabled,
            program.ReferrerRewardPoints,
            program.RefereeRewardPoints,
            program.MaxRewardsPerReferrer,
            program.CreatedAt,
            program.UpdatedAt);
}
