namespace Adeni.Application.Retention;

using Adeni.Domain.Retention;

/// <summary>API snake_case codes for customer membership enums.</summary>
public static class CustomerMembershipCodes
{
    public static string ToApi(CustomerMembershipStatus status) =>
        status switch
        {
            CustomerMembershipStatus.Pending => "pending",
            CustomerMembershipStatus.Active => "active",
            CustomerMembershipStatus.PastDue => "past_due",
            CustomerMembershipStatus.Frozen => "frozen",
            CustomerMembershipStatus.CancelPending => "cancel_pending",
            CustomerMembershipStatus.Cancelled => "cancelled",
            _ => "expired",
        };

    public static string ToApi(MembershipEnrolChannel channel) =>
        channel switch
        {
            MembershipEnrolChannel.InPersonTill => "in_person_till",
            MembershipEnrolChannel.InPersonTransfer => "in_person_transfer",
            _ => "online",
        };
}

/// <summary>In-person enrolment (till / transfer). Tenant and customer come from the route, never the body.</summary>
public sealed record EnrolMembershipRequest(
    Guid MembershipPlanId,
    Guid BusinessLocationId,
    string PaymentMethod,
    string? Note = null);

public sealed record CancelMembershipRequest(string? Reason = null);

/// <summary>
/// Plan terms are snapshots taken at enrolment. <c>Balances</c> are the credits of the current cycle only
/// (cycle number on each line); history stays in the ledger.
/// </summary>
public sealed record CustomerMembershipResponse(
    Guid Id,
    Guid TenantId,
    Guid TenantCustomerId,
    Guid MembershipPlanId,
    string Name,
    decimal Price,
    string Currency,
    string BillingInterval,
    string RolloverPolicy,
    string CancellationPolicy,
    bool AllowFreeze,
    int PastDueGraceDays,
    string Status,
    string EnrolChannel,
    Guid? EnrolledAtLocationId,
    int CurrentCycleNumber,
    DateTimeOffset? CurrentCycleStart,
    DateTimeOffset? CurrentCycleEnd,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? FrozenAt,
    DateTimeOffset? CancelRequestedAt,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    string? Note,
    IReadOnlyList<MembershipBenefitSnapshotResponse> Benefits,
    IReadOnlyList<MembershipCycleBalanceResponse> Balances,
    DateTimeOffset CreatedAt);

public sealed record MembershipBenefitSnapshotResponse(Guid ServiceOfferingId, string ServiceName, int QuantityPerCycle);

public sealed record MembershipCycleBalanceResponse(
    Guid Id,
    int CycleNumber,
    Guid ServiceOfferingId,
    string ServiceName,
    int Granted,
    int Used,
    int Reserved,
    int Forfeited,
    int Remaining,
    DateTimeOffset? ExpiresAt);

public sealed record CustomerMembershipListResponse(IReadOnlyList<CustomerMembershipResponse> Items);
