namespace Adeni.Infrastructure.Retention;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Adeni.Application.Retention;
using Adeni.Domain.Retention;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

internal static class RetentionHash
{
    public static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public static string KeyHash(Guid tenantId, string kind, string actor, string key) =>
        Sha256($"{tenantId:N}:{kind}:{actor}:{key}");

    public static string Fingerprint(params object?[] data) =>
        Sha256(JsonSerializer.Serialize(data));
}

internal static class RetentionMapper
{
    public static PackageDefinitionResponse ToResponse(PackageDefinition definition) =>
        new(
            definition.Id,
            definition.Name,
            definition.Description,
            definition.ListPrice,
            definition.Currency,
            definition.IsActive,
            definition.DefaultExpiryMonths,
            definition.EffectiveExpiryMonths,
            RetentionScopeCodes.ToApi(definition.RedemptionScope),
            RetentionScopeCodes.ToApi(definition.SellScope),
            LocationIdList.Parse(definition.RedemptionLocationIdsJson),
            LocationIdList.Parse(definition.SellLocationIdsJson),
            definition.Benefits
                .OrderBy(x => x.SortOrder)
                .Select(x => new PackageBenefitResponse(x.Id, x.ServiceOfferingId, x.ServiceName, x.QuantityGranted))
                .ToArray(),
            definition.CreatedAt,
            definition.UpdatedAt);

    public static MembershipPlanResponse ToResponse(MembershipPlan plan) =>
        new(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.Price,
            plan.Currency,
            MembershipPlanCodes.ToApi(plan.BillingInterval),
            MembershipPlanCodes.ToApi(plan.RolloverPolicy),
            MembershipPlanCodes.ToApi(plan.CancellationPolicy),
            plan.AllowFreeze,
            plan.PastDueGraceDays,
            plan.IsActive,
            plan.Benefits
                .OrderBy(x => x.SortOrder)
                .Select(x => new MembershipBenefitResponse(x.Id, x.ServiceOfferingId, x.ServiceName, x.QuantityPerCycle))
                .ToArray(),
            plan.CreatedAt,
            plan.UpdatedAt);

    public static CustomerMembershipResponse ToResponse(CustomerMembership membership) =>
        new(
            membership.Id,
            membership.TenantId,
            membership.TenantCustomerId,
            membership.MembershipPlanId,
            membership.NameSnapshot,
            membership.PriceSnapshot,
            membership.CurrencySnapshot,
            MembershipPlanCodes.ToApi(membership.BillingIntervalSnapshot),
            MembershipPlanCodes.ToApi(membership.RolloverPolicySnapshot),
            MembershipPlanCodes.ToApi(membership.CancellationPolicySnapshot),
            membership.AllowFreezeSnapshot,
            membership.PastDueGraceDaysSnapshot,
            CustomerMembershipCodes.ToApi(membership.Status),
            CustomerMembershipCodes.ToApi(membership.EnrolChannel),
            membership.EnrolledAtLocationId,
            membership.CurrentCycleNumber,
            membership.CurrentCycleStart,
            membership.CurrentCycleEnd,
            membership.ActivatedAt,
            membership.FrozenAt,
            membership.CancelRequestedAt,
            membership.CancelledAt,
            membership.CancelReason,
            membership.Note,
            membership.Benefits
                .OrderBy(x => x.SortOrder)
                .Select(x => new MembershipBenefitSnapshotResponse(x.ServiceOfferingId, x.ServiceName, x.QuantityPerCycle))
                .ToArray(),
            membership.Balances
                .Where(x => x.CycleNumber == membership.CurrentCycleNumber)
                .OrderBy(x => x.ServiceName)
                .Select(x => new MembershipCycleBalanceResponse(
                    x.Id, x.CycleNumber ?? 0, x.ServiceOfferingId, x.ServiceName, x.Granted, x.Used, x.Reserved,
                    x.Forfeited, x.Remaining, x.ExpiresAt))
                .ToArray(),
            membership.CreatedAt);

    public static CustomerPackageResponse ToResponse(
        CustomerPackage package,
        DateTimeOffset now,
        string? businessName = null)
    {
        // Read-time lazy expiry: an Active package past ExpiresAt is shown as expired even before the job runs.
        var status = package.Status == CustomerPackageStatus.Active && package.ExpiresAt is { } expires && expires <= now
            ? CustomerPackageStatus.Expired
            : package.Status;

        return new CustomerPackageResponse(
            package.Id,
            package.TenantId,
            package.TenantCustomerId,
            package.PackageDefinitionId,
            package.NameSnapshot,
            package.ListPriceSnapshot,
            package.CurrencySnapshot,
            RetentionScopeCodes.ToApi(status),
            RetentionScopeCodes.ToApi(package.SoldChannel),
            package.SoldAtLocationId,
            RetentionScopeCodes.ToApi(package.RedemptionScopeSnapshot),
            LocationIdList.Parse(package.RedemptionLocationIdsJson),
            package.ActivatedAt,
            package.ExpiresAt,
            package.VoidedAt,
            package.VoidReason,
            package.Note,
            package.Balances
                .OrderBy(x => x.ServiceName)
                .Select(x => new BenefitBalanceResponse(
                    x.Id, x.ServiceOfferingId, x.ServiceName, x.Granted, x.Used, x.Reserved, x.Forfeited, x.Remaining, x.ExpiresAt))
                .ToArray(),
            businessName);
    }
}

/// <summary>Ledger helpers shared by redemption, void and expiry (all stage changes; caller saves).</summary>
internal static class RetentionLedger
{
    public sealed record OpenReservation(Guid BookingId, int Quantity, Guid? LocationId);

    public static async Task<IReadOnlyList<OpenReservation>> OpenReservationsAsync(
        AdeniDbContext db,
        Guid tenantId,
        Guid balanceId,
        CancellationToken cancellationToken)
    {
        var rows = await db.Set<BenefitTransaction>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BenefitBalanceId == balanceId && x.BookingId != null)
            .ToListAsync(cancellationToken);

        return rows
            .Where(r => r.Kind == BenefitTransactionKind.Reserve
                && !rows.Any(o => o.BookingId == r.BookingId
                    && o.Kind is BenefitTransactionKind.Release or BenefitTransactionKind.Redeem))
            .Select(r => new OpenReservation(r.BookingId!.Value, r.Quantity, r.BusinessLocationId))
            .ToArray();
    }
}