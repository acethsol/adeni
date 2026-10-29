namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Retention;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class MembershipPlanService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    IRetentionServiceLookup services,
    TimeProvider clock,
    IAuditLogWriter? audit = null,
    ICorrelationContext? correlation = null) : IMembershipPlanService
{
    private static readonly HashSet<string> Currencies = ["NGN", "CAD", "USD"];
    private const int MaxBenefitLines = 10;

    public async Task<Result<MembershipPlanListResponse>> ListAsync(
        Guid tenantId, bool activeOnly, CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureMembershipsEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<MembershipPlanListResponse>(entitled.Error);
        }

        var query = db.Set<MembershipPlan>().AsNoTracking()
            .Include(x => x.Benefits)
            .Where(x => x.TenantId == tenantId);
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        var rows = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return Result.Success(new MembershipPlanListResponse(rows.Select(RetentionMapper.ToResponse).ToArray()));
    }

    public async Task<Result<MembershipPlanResponse>> GetAsync(
        Guid tenantId, Guid membershipPlanId, CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureMembershipsEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<MembershipPlanResponse>(entitled.Error);
        }

        var row = await db.Set<MembershipPlan>().AsNoTracking()
            .Include(x => x.Benefits)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == membershipPlanId, cancellationToken);
        return row is null
            ? Result.Failure<MembershipPlanResponse>(RetentionErrorCodes.MembershipPlanNotFoundError())
            : Result.Success(RetentionMapper.ToResponse(row));
    }

    public async Task<Result<MembershipPlanResponse>> CreateAsync(
        Guid tenantId, RetentionActor actor, CreateMembershipPlanRequest request, CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureMembershipsEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<MembershipPlanResponse>(entitled.Error);
        }

        var now = clock.GetUtcNow();
        var plan = new MembershipPlan
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var applied = await ApplyAsync(
            plan,
            request.Name,
            request.Description,
            request.Price,
            request.Currency,
            request.BillingInterval,
            request.RolloverPolicy,
            request.CancellationPolicy,
            request.AllowFreeze ?? false,
            request.PastDueGraceDays ?? MembershipPlan.DefaultPastDueGraceDays,
            request.Benefits,
            cancellationToken);
        if (applied.IsFailure)
        {
            return Result.Failure<MembershipPlanResponse>(applied.Error);
        }

        db.Add(plan);
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync(actor, AuditActions.RetentionMembershipPlanCreated, plan.Id, tenantId, cancellationToken);
        return Result.Success(RetentionMapper.ToResponse(plan));
    }

    public async Task<Result<MembershipPlanResponse>> UpdateAsync(
        Guid tenantId, RetentionActor actor, Guid membershipPlanId, UpdateMembershipPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureMembershipsEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<MembershipPlanResponse>(entitled.Error);
        }

        var plan = await db.Set<MembershipPlan>()
            .Include(x => x.Benefits)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == membershipPlanId, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<MembershipPlanResponse>(RetentionErrorCodes.MembershipPlanNotFoundError());
        }

        // PATCH semantics: null keeps the current value. Enrolled memberships snapshot their own terms (slice B).
        var applied = await ApplyAsync(
            plan,
            request.Name ?? plan.Name,
            request.Description ?? plan.Description,
            request.Price ?? plan.Price,
            request.Currency ?? plan.Currency,
            request.BillingInterval ?? MembershipPlanCodes.ToApi(plan.BillingInterval),
            request.RolloverPolicy ?? MembershipPlanCodes.ToApi(plan.RolloverPolicy),
            request.CancellationPolicy ?? MembershipPlanCodes.ToApi(plan.CancellationPolicy),
            request.AllowFreeze ?? plan.AllowFreeze,
            request.PastDueGraceDays ?? plan.PastDueGraceDays,
            request.Benefits?.ToArray(),
            cancellationToken);
        if (applied.IsFailure)
        {
            return Result.Failure<MembershipPlanResponse>(applied.Error);
        }

        plan.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await AuditAsync(actor, AuditActions.RetentionMembershipPlanUpdated, plan.Id, tenantId, cancellationToken);
        return Result.Success(RetentionMapper.ToResponse(plan));
    }

    public async Task<Result<MembershipPlanResponse>> DeactivateAsync(
        Guid tenantId, RetentionActor actor, Guid membershipPlanId, CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureMembershipsEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<MembershipPlanResponse>(entitled.Error);
        }

        var plan = await db.Set<MembershipPlan>()
            .Include(x => x.Benefits)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == membershipPlanId, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<MembershipPlanResponse>(RetentionErrorCodes.MembershipPlanNotFoundError());
        }

        if (plan.IsActive)
        {
            var now = clock.GetUtcNow();
            plan.IsActive = false;
            plan.DeactivatedAt = now;
            plan.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await AuditAsync(actor, AuditActions.RetentionMembershipPlanDeactivated, plan.Id, tenantId, cancellationToken);
        }

        return Result.Success(RetentionMapper.ToResponse(plan));
    }

    private async Task<Result> ApplyAsync(
        MembershipPlan plan,
        string? name,
        string? description,
        decimal price,
        string? currency,
        string? billingInterval,
        string? rolloverPolicy,
        string? cancellationPolicy,
        bool allowFreeze,
        int pastDueGraceDays,
        IReadOnlyList<MembershipBenefitInput>? benefits,
        CancellationToken cancellationToken)
    {
        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > 200)
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("Plan name must be 1–200 characters."));
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is { Length: > 1000 })
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("Description must be at most 1000 characters."));
        }

        if (price <= 0 || price > 9_999_999_999.99m || decimal.Round(price, 2) != price)
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("Price must be a positive amount with at most 2 decimals."));
        }

        var iso = currency?.Trim().ToUpperInvariant();
        if (iso is null || !Currencies.Contains(iso))
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("Unsupported currency."));
        }

        if (!MembershipPlanCodes.TryParseInterval(billingInterval, out var interval))
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("Unknown billing interval."));
        }

        if (!MembershipPlanCodes.TryParseRollover(rolloverPolicy, out var rollover))
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("Unknown rollover policy."));
        }

        if (!MembershipPlanCodes.TryParseCancellation(cancellationPolicy, out var cancellation))
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("Unknown cancellation policy."));
        }

        if (pastDueGraceDays is < 0 or > MembershipPlan.MaxPastDueGraceDays)
        {
            return Result.Failure(RetentionErrorCodes.ValidationError(
                $"Past-due grace must be between 0 and {MembershipPlan.MaxPastDueGraceDays} days."));
        }

        if (benefits is not null)
        {
            var lines = benefits.ToArray();
            if (lines.Length is < 1 or > MaxBenefitLines
                || lines.Any(x => x.QuantityPerCycle is < 1 or > 1000)
                || lines.Select(x => x.ServiceOfferingId).Distinct().Count() != lines.Length)
            {
                return Result.Failure(RetentionErrorCodes.ValidationError(
                    $"A plan needs 1–{MaxBenefitLines} distinct services with quantities of 1–1000 per cycle."));
            }

            var found = await services.FindAsync(plan.TenantId, lines.Select(x => x.ServiceOfferingId).ToArray(), cancellationToken);
            var byId = found.ToDictionary(x => x.Id);
            if (lines.Any(x => !byId.TryGetValue(x.ServiceOfferingId, out var svc) || !svc.IsActive))
            {
                return Result.Failure(RetentionErrorCodes.ValidationError("Every plan service must be an active service of this business."));
            }

            // Reconcile by service (unique per plan) instead of delete + insert.
            var existingByService = plan.Benefits.ToDictionary(x => x.ServiceOfferingId);
            foreach (var stale in existingByService.Values.Where(x => lines.All(l => l.ServiceOfferingId != x.ServiceOfferingId)))
            {
                db.Remove(stale);
            }

            for (var i = 0; i < lines.Length; i++)
            {
                var serviceName = byId[lines[i].ServiceOfferingId].Name;
                if (existingByService.TryGetValue(lines[i].ServiceOfferingId, out var current))
                {
                    current.QuantityPerCycle = lines[i].QuantityPerCycle;
                    current.ServiceName = serviceName;
                    current.SortOrder = i;
                    continue;
                }

                var added = new MembershipPlanBenefit
                {
                    Id = Guid.NewGuid(),
                    TenantId = plan.TenantId,
                    MembershipPlanId = plan.Id,
                    ServiceOfferingId = lines[i].ServiceOfferingId,
                    ServiceName = serviceName,
                    QuantityPerCycle = lines[i].QuantityPerCycle,
                    SortOrder = i,
                };
                plan.Benefits.Add(added);

                // A client-generated key on a child added to an already-tracked plan is detected as Modified
                // (UPDATE of a missing row). Mark it Added explicitly; new plans are added as a graph by the caller.
                if (db.Entry(plan).State != EntityState.Detached)
                {
                    db.Entry(added).State = EntityState.Added;
                }
            }
        }
        else if (plan.Benefits.Count == 0)
        {
            return Result.Failure(RetentionErrorCodes.ValidationError("A plan needs at least one service."));
        }

        plan.Name = trimmedName;
        plan.Description = trimmedDescription;
        plan.Price = price;
        plan.Currency = iso;
        plan.BillingInterval = interval;
        plan.RolloverPolicy = rollover;
        plan.CancellationPolicy = cancellation;
        plan.AllowFreeze = allowFreeze;
        plan.PastDueGraceDays = pastDueGraceDays;
        return Result.Success();
    }

    private Task AuditAsync(RetentionActor actor, string action, Guid id, Guid tenantId, CancellationToken cancellationToken) =>
        audit?.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actor.Id,
                action,
                "retention.membership_plan",
                id.ToString(),
                correlation?.CorrelationId ?? "background",
                clock.GetUtcNow(),
                $"{{\"tenantId\":\"{tenantId}\"}}"),
            cancellationToken) ?? Task.CompletedTask;
}
