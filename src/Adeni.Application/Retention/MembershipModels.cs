namespace Adeni.Application.Retention;

using Adeni.Domain.Retention;

/// <summary>API snake_case codes for membership plan enums.</summary>
public static class MembershipPlanCodes
{
    public const string Monthly = "monthly";
    public const string Quarterly = "quarterly";
    public const string Annually = "annually";
    public const string RolloverNone = "none";
    public const string RolloverCarryOverOneCycle = "carry_over_one_cycle";
    public const string CancelEndOfCycle = "end_of_cycle";
    public const string CancelImmediate = "immediate";

    public static string ToApi(MembershipBillingInterval interval) =>
        interval switch
        {
            MembershipBillingInterval.Quarterly => Quarterly,
            MembershipBillingInterval.Annually => Annually,
            _ => Monthly,
        };

    public static string ToApi(MembershipRolloverPolicy policy) =>
        policy == MembershipRolloverPolicy.CarryOverOneCycle ? RolloverCarryOverOneCycle : RolloverNone;

    public static string ToApi(MembershipCancellationPolicy policy) =>
        policy == MembershipCancellationPolicy.Immediate ? CancelImmediate : CancelEndOfCycle;

    public static bool TryParseInterval(string? value, out MembershipBillingInterval interval)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or Monthly:
                interval = MembershipBillingInterval.Monthly;
                return true;
            case Quarterly:
                interval = MembershipBillingInterval.Quarterly;
                return true;
            case Annually:
                interval = MembershipBillingInterval.Annually;
                return true;
            default:
                interval = default;
                return false;
        }
    }

    public static bool TryParseRollover(string? value, out MembershipRolloverPolicy policy)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or RolloverNone:
                policy = MembershipRolloverPolicy.None;
                return true;
            case RolloverCarryOverOneCycle:
                policy = MembershipRolloverPolicy.CarryOverOneCycle;
                return true;
            default:
                policy = default;
                return false;
        }
    }

    public static bool TryParseCancellation(string? value, out MembershipCancellationPolicy policy)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or CancelEndOfCycle:
                policy = MembershipCancellationPolicy.EndOfCycle;
                return true;
            case CancelImmediate:
                policy = MembershipCancellationPolicy.Immediate;
                return true;
            default:
                policy = default;
                return false;
        }
    }
}

public sealed record MembershipBenefitInput(Guid ServiceOfferingId, int QuantityPerCycle);

public sealed record MembershipBenefitResponse(
    Guid Id,
    Guid ServiceOfferingId,
    string ServiceName,
    int QuantityPerCycle);

/// <summary>Create body; <c>Benefits</c> required. Policies default to none / end_of_cycle / no freeze.</summary>
public sealed record CreateMembershipPlanRequest(
    string Name,
    decimal Price,
    string Currency,
    IReadOnlyList<MembershipBenefitInput> Benefits,
    string? Description = null,
    string? BillingInterval = null,
    string? RolloverPolicy = null,
    string? CancellationPolicy = null,
    bool? AllowFreeze = null,
    int? PastDueGraceDays = null);

/// <summary>PATCH body — null leaves a field unchanged. Enrolled memberships are never rewritten.</summary>
public sealed record UpdateMembershipPlanRequest(
    string? Name = null,
    string? Description = null,
    decimal? Price = null,
    string? Currency = null,
    string? BillingInterval = null,
    string? RolloverPolicy = null,
    string? CancellationPolicy = null,
    bool? AllowFreeze = null,
    int? PastDueGraceDays = null,
    IReadOnlyList<MembershipBenefitInput>? Benefits = null);

public sealed record MembershipPlanResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    string BillingInterval,
    string RolloverPolicy,
    string CancellationPolicy,
    bool AllowFreeze,
    int PastDueGraceDays,
    bool IsActive,
    IReadOnlyList<MembershipBenefitResponse> Benefits,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record MembershipPlanListResponse(IReadOnlyList<MembershipPlanResponse> Items);
