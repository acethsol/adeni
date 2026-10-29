namespace Adeni.Application.Retention;

using Adeni.Domain.Common;

/// <summary>
/// Customer memberships (Sprint 37.2-B, in-person only). Online checkout / renewals are the billing
/// coordinator (slice C); credits are granted through the 37.1 benefit ledger.
/// </summary>
public interface ICustomerMembershipService
{
    Task<Result<CustomerMembershipListResponse>> ListForCustomerAsync(
        Guid tenantId,
        Guid tenantCustomerId,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerMembershipResponse>> GetAsync(
        Guid tenantId,
        Guid customerMembershipId,
        CancellationToken cancellationToken = default);

    /// <summary>Enrols a roster customer at the till / by transfer and grants cycle 1 credits. <paramref name="idempotencyKey"/> is required.</summary>
    Task<Result<CustomerMembershipResponse>> EnrolAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid tenantCustomerId,
        EnrolMembershipRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Owner-only. Active → Frozen when the plan allows it. Repeating on a frozen membership is a no-op.</summary>
    Task<Result<CustomerMembershipResponse>> FreezeAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid customerMembershipId,
        CancellationToken cancellationToken = default);

    /// <summary>Owner-only. Frozen → Active. Repeating on an active membership is a no-op.</summary>
    Task<Result<CustomerMembershipResponse>> UnfreezeAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid customerMembershipId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Owner-only. Applies the plan's cancellation policy (immediate, or end of the paid cycle). Repeating on a
    /// cancelled / cancel-pending membership is a no-op.
    /// </summary>
    Task<Result<CustomerMembershipResponse>> CancelAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid customerMembershipId,
        CancelMembershipRequest request,
        CancellationToken cancellationToken = default);
}
