namespace Adeni.Infrastructure.Retention;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Adeni.Application.Events;
using Adeni.Application.Retention;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Domain.Retention.Events;
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
            membership.CreatedAt,
            membership.PastDueSince);

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

/// <summary>Membership cycle ledger helpers (Sprint 37.2-C). All stage changes on the caller's unit of work; caller saves.</summary>
internal static class MembershipCycles
{
    /// <summary>Grants the current cycle's credits from the enrolment snapshot lines (+ rolled-over credits per service).</summary>
    public static void GrantCurrentCycle(
        AdeniDbContext db,
        CustomerMembership membership,
        string actor,
        Guid? locationId,
        DateTimeOffset now,
        IReadOnlyDictionary<Guid, int>? carry = null)
    {
        foreach (var line in membership.Benefits.OrderBy(x => x.SortOrder))
        {
            var carried = carry is not null && carry.TryGetValue(line.ServiceOfferingId, out var value) ? value : 0;
            var balance = BenefitBalance.GrantForMembership(
                membership.TenantId, membership.Id, membership.CurrentCycleNumber, line.ServiceOfferingId, line.ServiceName,
                line.QuantityPerCycle, carried, actor, locationId, membership.CurrentCycleEnd!.Value, now, out var grant);
            // Explicit Add: the membership is already tracked and a client-set Guid key reached via a navigation
            // would otherwise be treated as Modified (UPDATE of a missing row).
            db.Add(balance);
            db.Add(grant);
        }
    }

    /// <summary>
    /// Ends the current cycle's credits: unreserved remainder is forfeited, except what the plan's rollover policy
    /// carries into the next cycle (returned per service). Reservations on future bookings are left to settle.
    /// </summary>
    public static Dictionary<Guid, int> CloseCurrentCycle(
        AdeniDbContext db, CustomerMembership membership, string actor, DateTimeOffset now)
    {
        var carry = new Dictionary<Guid, int>();
        foreach (var balance in membership.Balances.Where(x => x.CycleNumber == membership.CurrentCycleNumber))
        {
            var carried = membership.RolloverPolicySnapshot == MembershipRolloverPolicy.CarryOverOneCycle
                ? balance.CarryableToNextCycle
                : 0;
            if (carried > 0)
            {
                carry[balance.ServiceOfferingId] = carried;
            }

            var reason = carried > 0 ? $"membership_cycle_ended_carried_{carried}" : "membership_cycle_ended";
            if (balance.ForfeitRemaining(BenefitTransactionKind.Forfeit, actor, reason, now) is { } forfeit)
            {
                db.Add(forfeit);
            }
        }

        return carry;
    }

    /// <summary>Releases every open reservation and forfeits all remaining credits (cancel / expiry).</summary>
    public static async Task<Result> ReleaseAndForfeitAllAsync(
        AdeniDbContext db,
        IDomainEventCollector events,
        CustomerMembership membership,
        string actor,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var balance in membership.Balances)
        {
            foreach (var open in await RetentionLedger.OpenReservationsAsync(db, membership.TenantId, balance.Id, cancellationToken))
            {
                var released = balance.Release(open.Quantity, open.BookingId, open.LocationId, actor, reason, now);
                if (released.IsFailure)
                {
                    return Result.Failure(released.Error);
                }

                db.Add(released.Value!);
                events.Add(new BenefitReleased(balance.Id, membership.TenantId, open.BookingId, open.Quantity, now));
            }

            if (balance.ForfeitRemaining(BenefitTransactionKind.Forfeit, actor, reason, now) is { } forfeited)
            {
                db.Add(forfeited);
            }
        }

        return Result.Success();
    }
}