namespace Adeni.Application.Retention;

using Adeni.Domain.Common;

/// <summary>Membership plan catalog (Sprint 37.2-A). Every method requires the <c>memberships</c> plan entitlement.</summary>
public interface IMembershipPlanService
{
    Task<Result<MembershipPlanListResponse>> ListAsync(
        Guid tenantId,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<Result<MembershipPlanResponse>> GetAsync(
        Guid tenantId,
        Guid membershipPlanId,
        CancellationToken cancellationToken = default);

    Task<Result<MembershipPlanResponse>> CreateAsync(
        Guid tenantId,
        RetentionActor actor,
        CreateMembershipPlanRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<MembershipPlanResponse>> UpdateAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid membershipPlanId,
        UpdateMembershipPlanRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<MembershipPlanResponse>> DeactivateAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid membershipPlanId,
        CancellationToken cancellationToken = default);
}
