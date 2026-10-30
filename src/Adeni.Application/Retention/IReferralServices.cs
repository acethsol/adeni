namespace Adeni.Application.Retention;

using Adeni.Domain.Common;

public sealed record ReferralProgramResponse(
    bool Configured,
    Guid? Id,
    bool IsEnabled,
    int ReferrerRewardPoints,
    int RefereeRewardPoints,
    int? MaxRewardsPerReferrer,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record UpsertReferralProgramRequest(
    bool IsEnabled,
    int ReferrerRewardPoints,
    int RefereeRewardPoints,
    int? MaxRewardsPerReferrer = null);

public sealed record ReferralCodeResponse(
    Guid TenantCustomerId,
    string Code,
    string? SharePath,
    DateTimeOffset CreatedAt);

public interface IReferralProgramService
{
    Task<Result<ReferralProgramResponse>> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<Result<ReferralProgramResponse>> UpsertAsync(
        Guid tenantId,
        RetentionActor actor,
        UpsertReferralProgramRequest request,
        CancellationToken cancellationToken = default);
}

public interface IReferralCodeService
{
    Task<Result<ReferralCodeResponse>> GetOrCreateForCustomerAsync(
        Guid tenantId,
        Guid tenantCustomerId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Referral attach + qualification (Sprint 37.3-E). Stages rows on the caller's DbContext; caller commits.
/// Invalid referral codes at booking are ignored (booking still succeeds).
/// </summary>
public interface IReferralQualificationService
{
    Task TryAttachCodeForBookingAsync(
        Guid tenantId,
        Guid platformCustomerId,
        string? referralCode,
        Guid bookingId,
        CancellationToken cancellationToken = default);

    /// <summary>Idempotent per attribution / qualifying booking. Issues loyalty points when rules pass.</summary>
    Task<Result> TryQualifyAndIssueRewardsForPaidBookingAsync(
        Guid tenantId,
        Guid bookingId,
        string actor,
        CancellationToken cancellationToken = default);
}
