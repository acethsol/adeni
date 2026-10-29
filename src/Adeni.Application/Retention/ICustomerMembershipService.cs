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

    /// <summary>
    /// Staff records a till/transfer renewal for an <b>in-person</b> membership whose paid cycle has ended
    /// (still within past-due grace). Starts the next cycle and grants its credits. <paramref name="idempotencyKey"/> is required.
    /// </summary>
    Task<Result<CustomerMembershipResponse>> RenewAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid customerMembershipId,
        RenewMembershipRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>The signed-in customer's own memberships across businesses (excludes unpaid <c>pending</c> checkouts).</summary>
    Task<Result<CustomerMembershipListResponse>> ListForPlatformCustomerAsync(
        string customerAuth0Sub,
        CancellationToken cancellationToken = default);

    /// <summary>Customer self-service freeze (only when the plan snapshot allows it). Never touches another customer's membership.</summary>
    Task<Result<CustomerMembershipResponse>> FreezeForCustomerAsync(
        string customerAuth0Sub,
        Guid customerMembershipId,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerMembershipResponse>> UnfreezeForCustomerAsync(
        string customerAuth0Sub,
        Guid customerMembershipId,
        CancellationToken cancellationToken = default);

    /// <summary>Customer self-service cancel; applies the plan's cancellation policy (immediate or end of the paid cycle).</summary>
    Task<Result<CustomerMembershipResponse>> CancelForCustomerAsync(
        string customerAuth0Sub,
        Guid customerMembershipId,
        CancelMembershipRequest request,
        CancellationToken cancellationToken = default);
}
