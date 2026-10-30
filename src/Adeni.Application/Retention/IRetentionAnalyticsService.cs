namespace Adeni.Application.Retention;

using Adeni.Domain.Common;

/// <summary>Retention-owned aggregates (Sprint 37.5). Reporting and portal consume this port only — no direct EF from Reporting.</summary>
public interface IRetentionAnalyticsService
{
    Task<Result<TenantMembershipMrrResponse>> GetMembershipMrrAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result<RetentionAnalyticsSummaryResponse>> GetSummaryAsync(
        Guid tenantId,
        RetentionAnalyticsQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record RetentionAnalyticsQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    Guid? BusinessLocationId);

public sealed record PackageUtilizationMetrics(int Grants, int Redemptions, int Forfeits);

public sealed record LoyaltyPointsMetrics(int Earned, int Redeemed, int Expired, int Outstanding);

public sealed record RetentionAnalyticsSummaryResponse(
    TenantMembershipMrrResponse? MembershipMrr,
    PackageUtilizationMetrics Packages,
    LoyaltyPointsMetrics? Loyalty,
    DateTimeOffset AsOf);
