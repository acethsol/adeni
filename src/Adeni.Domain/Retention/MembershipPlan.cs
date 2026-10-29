namespace Adeni.Domain.Retention;

using Adeni.Domain.Tenancy;

/// <summary>
/// Recurring membership product (Sprint 37.2). Price is the list price of the recurring charge — never stored
/// value. Policies are typed columns (no rules DSL); they are enforced by later slices (enrolment / billing).
/// </summary>
public sealed class MembershipPlan : ITenantEntity
{
    public const int DefaultPastDueGraceDays = 7;
    public const int MaxPastDueGraceDays = 30;

    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "NGN";

    public MembershipBillingInterval BillingInterval { get; set; } = MembershipBillingInterval.Monthly;

    public MembershipRolloverPolicy RolloverPolicy { get; set; } = MembershipRolloverPolicy.None;

    public MembershipCancellationPolicy CancellationPolicy { get; set; } = MembershipCancellationPolicy.EndOfCycle;

    public bool AllowFreeze { get; set; }

    /// <summary>Days a membership may stay PastDue before it lapses (billing slice).</summary>
    public int PastDueGraceDays { get; set; } = DefaultPastDueGraceDays;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeactivatedAt { get; set; }

    public ICollection<MembershipPlanBenefit> Benefits { get; set; } = new List<MembershipPlanBenefit>();

    public override string ToString() => $"MembershipPlan {Id}";
}

/// <summary>One benefit line: N sessions of a catalog service granted each billing cycle.</summary>
public sealed class MembershipPlanBenefit : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid MembershipPlanId { get; set; }

    public Guid ServiceOfferingId { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public int QuantityPerCycle { get; set; }

    public int SortOrder { get; set; }
}
