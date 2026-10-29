namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Events;
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
    ICorrelationContext? correlation = null) : ICustomerMembershipService
{
    private const string EnrolKind = "membership_enrol";

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
        if (loaded.IsFailure)
        {
            return Result.Failure<CustomerMembershipResponse>(loaded.Error);
        }

        var membership = loaded.Value!;
        if (membership.Status == CustomerMembershipStatus.Frozen)
        {
            return Result.Success(RetentionMapper.ToResponse(membership));
        }

        var frozen = membership.Freeze(clock.GetUtcNow());
        if (frozen.IsFailure)
        {
            return Result.Failure<CustomerMembershipResponse>(frozen.Error);
        }

        return await SaveAsync(membership, actor, AuditActions.RetentionMembershipFrozen, tenantId, cancellationToken);
    }

    public async Task<Result<CustomerMembershipResponse>> UnfreezeAsync(
        Guid tenantId, RetentionActor actor, Guid customerMembershipId, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadForChangeAsync(tenantId, actor, customerMembershipId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<CustomerMembershipResponse>(loaded.Error);
        }

        var membership = loaded.Value!;
        if (membership.Status == CustomerMembershipStatus.Active)
        {
            return Result.Success(RetentionMapper.ToResponse(membership));
        }

        var thawed = membership.Unfreeze();
        if (thawed.IsFailure)
        {
            return Result.Failure<CustomerMembershipResponse>(thawed.Error);
        }

        return await SaveAsync(membership, actor, AuditActions.RetentionMembershipUnfrozen, tenantId, cancellationToken);
    }

    public async Task<Result<CustomerMembershipResponse>> CancelAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid customerMembershipId,
        CancelMembershipRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason is { Length: > 500 })
        {
            return Result.Failure<CustomerMembershipResponse>(
                RetentionErrorCodes.ValidationError("Reason must be at most 500 characters."));
        }

        var loaded = await LoadForChangeAsync(tenantId, actor, customerMembershipId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<CustomerMembershipResponse>(loaded.Error);
        }

        var membership = loaded.Value!;
        if (membership.Status is CustomerMembershipStatus.CancelPending or CustomerMembershipStatus.Cancelled)
        {
            return Result.Success(RetentionMapper.ToResponse(membership));
        }

        var now = clock.GetUtcNow();
        var cancelled = membership.RequestCancel(actor.Id, reason, now);
        if (cancelled.IsFailure)
        {
            return Result.Failure<CustomerMembershipResponse>(cancelled.Error);
        }

        var immediate = cancelled.Value;
        if (immediate)
        {
            foreach (var balance in membership.Balances)
            {
                foreach (var open in await RetentionLedger.OpenReservationsAsync(db, tenantId, balance.Id, cancellationToken))
                {
                    var released = balance.Release(open.Quantity, open.BookingId, open.LocationId, actor.Id, "membership_cancelled", now);
                    if (released.IsFailure)
                    {
                        return Result.Failure<CustomerMembershipResponse>(released.Error);
                    }

                    db.Add(released.Value!);
                    events.Add(new BenefitReleased(balance.Id, tenantId, open.BookingId, open.Quantity, now));
                }

                if (balance.ForfeitRemaining(BenefitTransactionKind.Forfeit, actor.Id, "membership_cancelled", now) is { } forfeited)
                {
                    db.Add(forfeited);
                }
            }
        }

        events.Add(new CustomerMembershipCancelled(membership.Id, tenantId, immediate, now));
        return await SaveAsync(membership, actor, AuditActions.RetentionMembershipCancelled, tenantId, cancellationToken);
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

    private async Task<Result<CustomerMembershipResponse>> SaveAsync(
        CustomerMembership membership, RetentionActor actor, string auditAction, Guid tenantId, CancellationToken cancellationToken)
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

        await AuditAsync(actor.Id, auditAction, membership.Id, tenantId, cancellationToken);
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
