namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Events;
using Adeni.Application.Finance;
using Adeni.Application.Retention;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Domain.Retention.Events;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerMembershipService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    IDomainEventCollector events,
    TimeProvider clock,
    IAuditLogWriter? audit = null,
    ICorrelationContext? correlation = null,
    IPaymentOrchestrator? payments = null) : ICustomerMembershipService
{
    private const string EnrolKind = "membership_enrol";
    private const string RenewKind = "membership_renew";

    public async Task<Result<CustomerMembershipListResponse>> ListForCustomerAsync(
        Guid tenantId, Guid tenantCustomerId, CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureMembershipsEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<CustomerMembershipListResponse>(entitled.Error);
        }

        var exists = await db.TenantCustomers.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == tenantCustomerId, cancellationToken);
        if (!exists)
        {
            return Result.Failure<CustomerMembershipListResponse>(Error.NotFound("Customer"));
        }

        var rows = await db.Set<CustomerMembership>().AsNoTracking()
            .Include(x => x.Benefits)
            .Include(x => x.Balances)
            .Where(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Result.Success(new CustomerMembershipListResponse(rows.Select(RetentionMapper.ToResponse).ToArray()));
    }

    public async Task<Result<CustomerMembershipResponse>> GetAsync(
        Guid tenantId, Guid customerMembershipId, CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureMembershipsEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<CustomerMembershipResponse>(entitled.Error);
        }

        return await LoadResponseAsync(tenantId, customerMembershipId, cancellationToken);
    }

    public async Task<Result<CustomerMembershipResponse>> EnrolAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid tenantCustomerId,
        EnrolMembershipRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureMembershipsEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<CustomerMembershipResponse>(entitled.Error);
        }

        var key = Adeni.Application.Common.IdempotencyKeyNormalizer.Normalize(idempotencyKey);
        if (key is null)
        {
            return Result.Failure<CustomerMembershipResponse>(RetentionErrorCodes.IdempotencyKeyRequiredError());
        }

        var method = request.PaymentMethod?.Trim().ToLowerInvariant();
        var channel = method switch
        {
            SellPaymentMethods.Till => MembershipEnrolChannel.InPersonTill,
            SellPaymentMethods.Transfer => MembershipEnrolChannel.InPersonTransfer,
            _ => (MembershipEnrolChannel?)null,
        };
        if (channel is null)
        {
            return Result.Failure<CustomerMembershipResponse>(
                RetentionErrorCodes.ValidationError("Payment method must be till or transfer."));
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > 500 })
        {
            return Result.Failure<CustomerMembershipResponse>(
                RetentionErrorCodes.ValidationError("Note must be at most 500 characters."));
        }

        var keyHash = RetentionHash.KeyHash(tenantId, EnrolKind, actor.Id, key);
        var fingerprint = RetentionHash.Fingerprint(
            request.MembershipPlanId, tenantCustomerId, request.BusinessLocationId, method, note);

        var replay = await ReplayAsync(tenantId, keyHash, fingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay.Value;
        }

        var customerExists = await db.TenantCustomers.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == tenantCustomerId, cancellationToken);
        if (!customerExists)
        {
            return Result.Failure<CustomerMembershipResponse>(Error.NotFound("Customer"));
        }

        var plan = await db.Set<MembershipPlan>().AsNoTracking()
            .Include(x => x.Benefits)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.MembershipPlanId, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<CustomerMembershipResponse>(RetentionErrorCodes.MembershipPlanNotFoundError());
        }

        if (!plan.IsActive)
        {
            return Result.Failure<CustomerMembershipResponse>(RetentionErrorCodes.MembershipPlanInactiveError());
        }

        var location = await TenantLocationScope.ValidateOptionalLocationIdAsync(
            db, tenantId, request.BusinessLocationId, cancellationToken);
        if (location.IsFailure || location.Value is null)
        {
            return Result.Failure<CustomerMembershipResponse>(
                location.IsFailure ? location.Error : RetentionErrorCodes.ValidationError("An enrolment location is required."));
        }

        var liveStatuses = new[]
        {
            CustomerMembershipStatus.Pending, CustomerMembershipStatus.Active, CustomerMembershipStatus.PastDue,
            CustomerMembershipStatus.Frozen, CustomerMembershipStatus.CancelPending,
        };
        var alreadyLive = await db.Set<CustomerMembership>().AsNoTracking()
            .AnyAsync(
                x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId
                    && x.MembershipPlanId == plan.Id && liveStatuses.Contains(x.Status),
                cancellationToken);
        if (alreadyLive)
        {
            return Result.Failure<CustomerMembershipResponse>(RetentionErrorCodes.MembershipAlreadyActiveError());
        }

        var now = clock.GetUtcNow();
        var membership = new CustomerMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenantCustomerId = tenantCustomerId,
            MembershipPlanId = plan.Id,
            EnrolledAtLocationId = location.Value,
            EnrolChannel = channel.Value,
            NameSnapshot = plan.Name,
            PriceSnapshot = plan.Price,
            CurrencySnapshot = plan.Currency,
            BillingIntervalSnapshot = plan.BillingInterval,
            RolloverPolicySnapshot = plan.RolloverPolicy,
            CancellationPolicySnapshot = plan.CancellationPolicy,
            AllowFreezeSnapshot = plan.AllowFreeze,
            PastDueGraceDaysSnapshot = plan.PastDueGraceDays,
            Note = note,
            EnrolledBy = actor.Id,
            CreatedAt = now,
        };
        // In-person enrolment is paid at the till / by transfer outside Adeni, so cycle 1 starts now (no pro-rating).
        membership.Activate(now);
        db.Add(membership);

        foreach (var line in plan.Benefits.OrderBy(x => x.SortOrder))
        {
            membership.Benefits.Add(new CustomerMembershipBenefit
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerMembershipId = membership.Id,
                ServiceOfferingId = line.ServiceOfferingId,
                ServiceName = line.ServiceName,
                QuantityPerCycle = line.QuantityPerCycle,
                SortOrder = line.SortOrder,
            });

            var balance = BenefitBalance.GrantForMembership(
                tenantId, membership.Id, membership.CurrentCycleNumber, line.ServiceOfferingId, line.ServiceName,
                line.QuantityPerCycle, actor.Id, location.Value, membership.CurrentCycleEnd!.Value, now, out var grant);
            membership.Balances.Add(balance);
            db.Add(grant);
        }

        db.Add(new BenefitOperation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            KeyHash = keyHash,
            Fingerprint = fingerprint,
            Kind = EnrolKind,
            Actor = actor.Id,
            ResultCustomerMembershipId = membership.Id,
            OccurredAt = now,
        });
        events.Add(new CustomerMembershipActivated(membership.Id, tenantId, tenantCustomerId, now));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race on the idempotency key (replay) or on the one-live-membership-per-plan index.
            db.ChangeTracker.Clear();
            events.TakeAll();
            var winner = await ReplayAsync(tenantId, keyHash, fingerprint, cancellationToken);
            return winner ?? Result.Failure<CustomerMembershipResponse>(RetentionErrorCodes.MembershipAlreadyActiveError());
        }

        await AuditAsync(actor.Id, AuditActions.RetentionMembershipEnrolled, membership.Id, tenantId, cancellationToken);
        return Result.Success(RetentionMapper.ToResponse(membership));
    }

    public async Task<Result<CustomerMembershipResponse>> FreezeAsync(
        Guid tenantId, RetentionActor actor, Guid customerMembershipId, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadForChangeAsync(tenantId, actor, customerMembershipId, cancellationToken);
        return loaded.IsFailure
            ? Result.Failure<CustomerMembershipResponse>(loaded.Error)
            : await ApplyFreezeAsync(loaded.Value!, actor.Id, cancellationToken);
    }

    public async Task<Result<CustomerMembershipResponse>> UnfreezeAsync(
        Guid tenantId, RetentionActor actor, Guid customerMembershipId, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadForChangeAsync(tenantId, actor, customerMembershipId, cancellationToken);
        return loaded.IsFailure
            ? Result.Failure<CustomerMembershipResponse>(loaded.Error)
            : await ApplyUnfreezeAsync(loaded.Value!, actor.Id, cancellationToken);
    }

    public async Task<Result<CustomerMembershipResponse>> CancelAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid customerMembershipId,
        CancelMembershipRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = NormalizeReason(request, out var invalid);
        if (invalid is not null)
        {
            return Result.Failure<CustomerMembershipResponse>(invalid);
        }

        var loaded = await LoadForChangeAsync(tenantId, actor, customerMembershipId, cancellationToken);
        return loaded.IsFailure
            ? Result.Failure<CustomerMembershipResponse>(loaded.Error)
            : await ApplyCancelAsync(loaded.Value!, actor.Id, reason, cancellationToken);
    }

    public async Task<Result<CustomerMembershipResponse>> RenewAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid customerMembershipId,
        RenewMembershipRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsureMembershipsEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<CustomerMembershipResponse>(entitled.Error);
        }

        var key = Adeni.Application.Common.IdempotencyKeyNormalizer.Normalize(idempotencyKey);
        if (key is null)
        {
            return Result.Failure<CustomerMembershipResponse>(RetentionErrorCodes.IdempotencyKeyRequiredError());
        }

        var method = request.PaymentMethod?.Trim().ToLowerInvariant();
        if (method is not (SellPaymentMethods.Till or SellPaymentMethods.Transfer))
        {
            return Result.Failure<CustomerMembershipResponse>(
                RetentionErrorCodes.ValidationError("Payment method must be till or transfer."));
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > 500 })
        {
            return Result.Failure<CustomerMembershipResponse>(
                RetentionErrorCodes.ValidationError("Note must be at most 500 characters."));
        }

        var keyHash = RetentionHash.KeyHash(tenantId, RenewKind, actor.Id, key);
        var fingerprint = RetentionHash.Fingerprint(customerMembershipId, method, note);
        var replay = await ReplayAsync(tenantId, keyHash, fingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay.Value;
        }

        var membership = await db.Set<CustomerMembership>()
            .Include(x => x.Benefits)
            .Include(x => x.Balances)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == customerMembershipId, cancellationToken);
        if (membership is null)
        {
            return Result.Failure<CustomerMembershipResponse>(RetentionErrorCodes.MembershipNotFoundError());
        }

        var now = clock.GetUtcNow();
        // Online memberships renew only through confirmed provider payments. In-person ones are renewed by staff
        // once the paid cycle has ended and while the plan's grace is still running.
        if (membership.IsOnline
            || membership.Status is not (CustomerMembershipStatus.Active or CustomerMembershipStatus.PastDue)
            || membership.CurrentCycleEnd is not { } end
            || now < end
            || membership.GraceElapsed(now))
        {
            return Result.Failure<CustomerMembershipResponse>(RetentionErrorCodes.MembershipRenewalNotDueError());
        }

        var carry = MembershipCycles.CloseCurrentCycle(db, membership, actor.Id, now);
        var renewed = membership.ApplyRenewal(now);
        if (renewed.IsFailure)
        {
            db.ChangeTracker.Clear();
            events.TakeAll();
            return Result.Failure<CustomerMembershipResponse>(renewed.Error);
        }

        MembershipCycles.GrantCurrentCycle(db, membership, actor.Id, membership.EnrolledAtLocationId, now, carry);
        db.Add(new BenefitOperation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            KeyHash = keyHash,
            Fingerprint = fingerprint,
            Kind = RenewKind,
            Actor = actor.Id,
            ResultCustomerMembershipId = membership.Id,
            OccurredAt = now,
        });
        events.Add(new CustomerMembershipRenewed(
            membership.Id, tenantId, membership.TenantCustomerId, membership.CurrentCycleNumber, now));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race on the idempotency key (replay) or on the cycle (another renewal applied first).
            db.ChangeTracker.Clear();
            events.TakeAll();
            var winner = await ReplayAsync(tenantId, keyHash, fingerprint, cancellationToken);
            return winner ?? Result.Failure<CustomerMembershipResponse>(RetentionErrorCodes.BenefitConflictError());
        }

        await AuditAsync(actor.Id, AuditActions.RetentionMembershipRenewed, membership.Id, tenantId, cancellationToken);
        return Result.Success(RetentionMapper.ToResponse(membership));
    }

    public async Task<Result<CustomerMembershipListResponse>> ListForPlatformCustomerAsync(
        string customerAuth0Sub, CancellationToken cancellationToken = default)
    {
        var tenantCustomerIds = await OwnTenantCustomerIdsAsync(customerAuth0Sub, cancellationToken);
        if (tenantCustomerIds.IsFailure)
        {
            return Result.Failure<CustomerMembershipListResponse>(tenantCustomerIds.Error);
        }

        var own = tenantCustomerIds.Value!.ToArray();
        if (own.Length == 0)
        {
            return Result.Success(new CustomerMembershipListResponse([]));
        }

        var rows = await db.Set<CustomerMembership>().AsNoTracking()
            .Include(x => x.Benefits)
            .Include(x => x.Balances)
            .Where(x => own.Contains(x.TenantCustomerId) && x.Status != CustomerMembershipStatus.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var tenantIds = rows.Select(x => x.TenantId).Distinct().ToArray();
        var names = await db.Tenants.AsNoTracking()
            .Where(x => tenantIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        // Open renewal charge, so a past-due member can pay without waiting for staff.
        var dueIntents = new Dictionary<Guid, Guid>();
        if (payments is not null)
        {
            var onlineIds = rows
                .Where(x => x.IsOnline && x.Status is CustomerMembershipStatus.Active or CustomerMembershipStatus.PastDue)
                .Select(x => x.Id)
                .ToArray();
            if (onlineIds.Length > 0)
            {
                var open = await db.Set<MembershipPayment>().AsNoTracking()
                    .Where(x => onlineIds.Contains(x.CustomerMembershipId)
                        && x.Kind == MembershipPaymentKind.Renewal && x.Status == MembershipPaymentStatus.Pending)
                    .OrderBy(x => x.CreatedAt)
                    .ToListAsync(cancellationToken);
                foreach (var row in open)
                {
                    dueIntents[row.CustomerMembershipId] = row.PaymentIntentId;
                }
            }
        }

        var items = new List<CustomerMembershipResponse>(rows.Count);
        foreach (var row in rows)
        {
            string? checkoutUrl = null;
            if (dueIntents.TryGetValue(row.Id, out var intentId) && payments is not null)
            {
                var intent = await payments.GetAsync(intentId, cancellationToken);
                checkoutUrl = intent.IsSuccess && !string.IsNullOrWhiteSpace(intent.Value!.CheckoutUrl)
                    ? intent.Value.CheckoutUrl
                    : null;
            }

            items.Add(RetentionMapper.ToResponse(row) with
            {
                BusinessName = names.GetValueOrDefault(row.TenantId),
                PaymentDueCheckoutUrl = checkoutUrl,
            });
        }

        return Result.Success(new CustomerMembershipListResponse(items));
    }

    public async Task<Result<CustomerMembershipResponse>> FreezeForCustomerAsync(
        string customerAuth0Sub, Guid customerMembershipId, CancellationToken cancellationToken = default)
    {
        var owned = await LoadOwnedAsync(customerAuth0Sub, customerMembershipId, cancellationToken);
        return owned.IsFailure
            ? Result.Failure<CustomerMembershipResponse>(owned.Error)
            : await ApplyFreezeAsync(owned.Value!, $"customer:{customerAuth0Sub}", cancellationToken);
    }

    public async Task<Result<CustomerMembershipResponse>> UnfreezeForCustomerAsync(
        string customerAuth0Sub, Guid customerMembershipId, CancellationToken cancellationToken = default)
    {
        var owned = await LoadOwnedAsync(customerAuth0Sub, customerMembershipId, cancellationToken);
        return owned.IsFailure
            ? Result.Failure<CustomerMembershipResponse>(owned.Error)
            : await ApplyUnfreezeAsync(owned.Value!, $"customer:{customerAuth0Sub}", cancellationToken);
    }

    public async Task<Result<CustomerMembershipResponse>> CancelForCustomerAsync(
        string customerAuth0Sub,
        Guid customerMembershipId,
        CancelMembershipRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = NormalizeReason(request, out var invalid);
        if (invalid is not null)
        {
            return Result.Failure<CustomerMembershipResponse>(invalid);
        }

        var owned = await LoadOwnedAsync(customerAuth0Sub, customerMembershipId, cancellationToken);
        return owned.IsFailure
            ? Result.Failure<CustomerMembershipResponse>(owned.Error)
            : await ApplyCancelAsync(owned.Value!, $"customer:{customerAuth0Sub}", reason, cancellationToken);
    }

    // ---- shared actions (membership already loaded + authorised) ----

    private async Task<Result<CustomerMembershipResponse>> ApplyFreezeAsync(
        CustomerMembership membership, string actorId, CancellationToken cancellationToken)
    {
        if (membership.Status == CustomerMembershipStatus.Frozen)
        {
            return Result.Success(RetentionMapper.ToResponse(membership));
        }

        var frozen = membership.Freeze(clock.GetUtcNow());
        return frozen.IsFailure
            ? Result.Failure<CustomerMembershipResponse>(frozen.Error)
            : await SaveAsync(membership, actorId, AuditActions.RetentionMembershipFrozen, cancellationToken);
    }

    private async Task<Result<CustomerMembershipResponse>> ApplyUnfreezeAsync(
        CustomerMembership membership, string actorId, CancellationToken cancellationToken)
    {
        if (membership.Status == CustomerMembershipStatus.Active)
        {
            return Result.Success(RetentionMapper.ToResponse(membership));
        }

        var thawed = membership.Unfreeze(clock.GetUtcNow());
        if (thawed.IsFailure)
        {
            return Result.Failure<CustomerMembershipResponse>(thawed.Error);
        }

        // The freeze extended the paid cycle; the current cycle's credits live as long as the cycle does.
        foreach (var balance in membership.Balances.Where(x => x.CycleNumber == membership.CurrentCycleNumber))
        {
            balance.ExpiresAt = membership.CurrentCycleEnd;
        }

        return await SaveAsync(membership, actorId, AuditActions.RetentionMembershipUnfrozen, cancellationToken);
    }

    private async Task<Result<CustomerMembershipResponse>> ApplyCancelAsync(
        CustomerMembership membership, string actorId, string? reason, CancellationToken cancellationToken)
    {
        if (membership.Status is CustomerMembershipStatus.CancelPending or CustomerMembershipStatus.Cancelled)
        {
            return Result.Success(RetentionMapper.ToResponse(membership));
        }

        var now = clock.GetUtcNow();
        var cancelled = membership.RequestCancel(actorId, reason, now);
        if (cancelled.IsFailure)
        {
            return Result.Failure<CustomerMembershipResponse>(cancelled.Error);
        }

        var immediate = cancelled.Value;
        if (immediate)
        {
            var forfeited = await MembershipCycles.ReleaseAndForfeitAllAsync(
                db, events, membership, actorId, "membership_cancelled", now, cancellationToken);
            if (forfeited.IsFailure)
            {
                return Result.Failure<CustomerMembershipResponse>(forfeited.Error);
            }
        }

        // End-of-cycle cancellations are scheduled here and closed by the billing sweep at CurrentCycleEnd.
        events.Add(new CustomerMembershipCancelled(membership.Id, membership.TenantId, immediate, now));
        return await SaveAsync(membership, actorId, AuditActions.RetentionMembershipCancelled, cancellationToken);
    }

    private static string? NormalizeReason(CancelMembershipRequest request, out Error? invalid)
    {
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        invalid = reason is { Length: > 500 }
            ? RetentionErrorCodes.ValidationError("Reason must be at most 500 characters.")
            : null;
        return reason;
    }

    private async Task<Result<CustomerMembership>> LoadForChangeAsync(
        Guid tenantId, RetentionActor actor, Guid customerMembershipId, CancellationToken cancellationToken)
    {
        var entitled = await entitlements.EnsureMembershipsEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<CustomerMembership>(entitled.Error);
        }

        if (!actor.IsOwner)
        {
            return Result.Failure<CustomerMembership>(RetentionErrorCodes.OwnerRequiredError());
        }

        var membership = await db.Set<CustomerMembership>()
            .Include(x => x.Benefits)
            .Include(x => x.Balances)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == customerMembershipId, cancellationToken);
        return membership is null
            ? Result.Failure<CustomerMembership>(RetentionErrorCodes.MembershipNotFoundError())
            : Result.Success(membership);
    }

    /// <summary>Loads a membership only if it belongs to the signed-in customer; anything else is "not found".</summary>
    private async Task<Result<CustomerMembership>> LoadOwnedAsync(
        string customerAuth0Sub, Guid customerMembershipId, CancellationToken cancellationToken)
    {
        var ids = await OwnTenantCustomerIdsAsync(customerAuth0Sub, cancellationToken);
        if (ids.IsFailure)
        {
            return Result.Failure<CustomerMembership>(ids.Error);
        }

        var own = ids.Value!.ToArray();
        var membership = await db.Set<CustomerMembership>()
            .Include(x => x.Benefits)
            .Include(x => x.Balances)
            .FirstOrDefaultAsync(
                x => x.Id == customerMembershipId && own.Contains(x.TenantCustomerId)
                    && x.Status != CustomerMembershipStatus.Pending,
                cancellationToken);
        return membership is null
            ? Result.Failure<CustomerMembership>(RetentionErrorCodes.MembershipNotFoundError())
            : Result.Success(membership);
    }

    /// <summary>Strictly the signed-in customer's own roster rows; never trusts a client-supplied tenant.</summary>
    private async Task<Result<IReadOnlyList<Guid>>> OwnTenantCustomerIdsAsync(
        string customerAuth0Sub, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<IReadOnlyList<Guid>>(ErrorCodes.CustomerAuthRequiredError());
        }

        var ids = await (
            from tc in db.TenantCustomers.AsNoTracking()
            join c in db.Customers.AsNoTracking() on tc.CustomerId equals c.Id
            where c.Auth0Sub == customerAuth0Sub
            select tc.Id).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<Guid>>(ids);
    }

    private async Task<Result<CustomerMembershipResponse>> SaveAsync(
        CustomerMembership membership, string actorId, string auditAction, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            events.TakeAll();
            return Result.Failure<CustomerMembershipResponse>(RetentionErrorCodes.BenefitConflictError());
        }

        await AuditAsync(actorId, auditAction, membership.Id, membership.TenantId, cancellationToken);
        return Result.Success(RetentionMapper.ToResponse(membership));
    }

    private async Task<Result<CustomerMembershipResponse>> LoadResponseAsync(
        Guid tenantId, Guid customerMembershipId, CancellationToken cancellationToken)
    {
        var row = await db.Set<CustomerMembership>().AsNoTracking()
            .Include(x => x.Benefits)
            .Include(x => x.Balances)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == customerMembershipId, cancellationToken);
        return row is null
            ? Result.Failure<CustomerMembershipResponse>(RetentionErrorCodes.MembershipNotFoundError())
            : Result.Success(RetentionMapper.ToResponse(row));
    }

    private async Task<Result<CustomerMembershipResponse>?> ReplayAsync(
        Guid tenantId, string keyHash, string fingerprint, CancellationToken cancellationToken)
    {
        var operation = await db.Set<BenefitOperation>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.KeyHash == keyHash, cancellationToken);
        if (operation is null)
        {
            return null;
        }

        if (operation.Fingerprint != fingerprint || operation.ResultCustomerMembershipId is null)
        {
            return Result.Failure<CustomerMembershipResponse>(RetentionErrorCodes.IdempotencyConflictError());
        }

        return await LoadResponseAsync(tenantId, operation.ResultCustomerMembershipId.Value, cancellationToken);
    }

    private Task AuditAsync(string actorId, string action, Guid id, Guid tenantId, CancellationToken cancellationToken) =>
        audit?.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actorId,
                action,
                "retention.customer_membership",
                id.ToString(),
                correlation?.CorrelationId ?? "background",
                clock.GetUtcNow(),
                $"{{\"tenantId\":\"{tenantId}\"}}"),
            cancellationToken) ?? Task.CompletedTask;
}