namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Common;
using Adeni.Application.Events;
using Adeni.Application.Finance;
using Adeni.Application.Retention;
using Adeni.Application.Tenancy;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Domain.Retention.Events;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provider-billed memberships (Sprint 37.2-C). Orchestration only — no wallet, no stored value, no card data:
/// <list type="bullet">
/// <item>Every billing cycle is one Finance payment intent tracked by a <see cref="MembershipPayment"/> row.</item>
/// <item>A cycle is applied <b>only</b> from the confirmed-payment signal and at most once (status guard +
/// unique indexes + rowversion): duplicate / concurrent deliveries are no-ops (Murphy).</item>
/// <item>Online checkout is stub/sandbox only until Sprint 34 merchant settlement exists (503 elsewhere).</item>
/// </list>
/// </summary>
public sealed class MembershipBillingCoordinator(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    IPaymentOrchestrator payments,
    IPaymentProvider provider,
    ITenantCustomerService tenantCustomers,
    IDomainEventCollector events,
    IHostEnvironment environment,
    TimeProvider clock,
    ILogger<MembershipBillingCoordinator> logger,
    IAuditLogWriter? audit = null,
    ICorrelationContext? correlation = null) : IMembershipBillingCoordinator
{
    private const string SubscribeKind = "membership_subscribe";
    private const string SystemActor = "system:membership-billing";
    private const int MaxRenewalAttempts = 5;
    private static readonly TimeSpan RenewalRetryInterval = TimeSpan.FromHours(24);

    // No live fallback to platform-only provider charges: Sprint 34 must supply merchant settlement first.
    public bool OnlineBillingEnabled =>
        (environment.IsDevelopment() || environment.EnvironmentName == "Testing") && provider.ProviderName == "Stub";

    // ---- Subscribe (customer) ----

    public async Task<Result<MembershipSubscribeResponse>> StartOnlineSubscriptionAsync(
        Guid tenantId,
        string customerAuth0Sub,
        Guid membershipPlanId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<MembershipSubscribeResponse>(ErrorCodes.CustomerAuthRequiredError());
        }

        var key = IdempotencyKeyNormalizer.Normalize(idempotencyKey);
        if (key is null)
        {
            return Result.Failure<MembershipSubscribeResponse>(RetentionErrorCodes.IdempotencyKeyRequiredError());
        }

        var entitled = await entitlements.EnsureMembershipsEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<MembershipSubscribeResponse>(entitled.Error);
        }

        var recurring = await entitlements.EnsureRecurringBillingEntitledAsync(tenantId, cancellationToken);
        if (recurring.IsFailure)
        {
            return Result.Failure<MembershipSubscribeResponse>(recurring.Error);
        }

        if (!OnlineBillingEnabled)
        {
            return Result.Failure<MembershipSubscribeResponse>(RetentionErrorCodes.OnlineSubscriptionUnavailableError());
        }

        var customerId = await db.Customers.AsNoTracking()
            .Where(x => x.Auth0Sub == customerAuth0Sub)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (customerId is null)
        {
            return Result.Failure<MembershipSubscribeResponse>(ErrorCodes.CustomerAuthRequiredError());
        }

        var keyHash = RetentionHash.KeyHash(tenantId, SubscribeKind, customerAuth0Sub, key);
        var fingerprint = RetentionHash.Fingerprint(membershipPlanId, customerId);

        var replay = await ReplayAsync(tenantId, keyHash, fingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay.Value;
        }

        var plan = await db.Set<MembershipPlan>().AsNoTracking()
            .Include(x => x.Benefits)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == membershipPlanId, cancellationToken);
        if (plan is null)
        {
            return Result.Failure<MembershipSubscribeResponse>(RetentionErrorCodes.MembershipPlanNotFoundError());
        }

        if (!plan.IsActive)
        {
            return Result.Failure<MembershipSubscribeResponse>(RetentionErrorCodes.MembershipPlanInactiveError());
        }

        var tenantCustomerId = await db.TenantCustomers.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerId == customerId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (tenantCustomerId is null)
        {
            // Roster row is created on first subscribe, same as the first booking / package purchase would.
            var ensured = await tenantCustomers.EnsureFromBookingAsync(tenantId, customerId.Value, cancellationToken);
            if (ensured.IsFailure)
            {
                return Result.Failure<MembershipSubscribeResponse>(ensured.Error);
            }

            tenantCustomerId = ensured.Value!.TenantCustomerId;
        }

        var now = clock.GetUtcNow();
        var live = await db.Set<CustomerMembership>()
            .Where(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId.Value && x.MembershipPlanId == plan.Id)
            .ToListAsync(cancellationToken);
        var existing = live.FirstOrDefault(x => CustomerMembership.IsLive(x.Status));
        if (existing is not null)
        {
            if (existing.Status != CustomerMembershipStatus.Pending || !existing.IsOnline)
            {
                return Result.Failure<MembershipSubscribeResponse>(RetentionErrorCodes.MembershipAlreadyActiveError());
            }

            // Abandoned checkout: resume it (and bind this key) instead of blocking the customer.
            db.Add(NewOperation(tenantId, keyHash, fingerprint, customerAuth0Sub, existing.Id, now));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                var winner = await ReplayAsync(tenantId, keyHash, fingerprint, cancellationToken);
                return winner ?? Result.Failure<MembershipSubscribeResponse>(RetentionErrorCodes.BenefitConflictError());
            }

            return await EnsureInitialCheckoutAsync(existing, cancellationToken);
        }

        var membership = new CustomerMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenantCustomerId = tenantCustomerId.Value,
            MembershipPlanId = plan.Id,
            EnrolChannel = MembershipEnrolChannel.Online,
            Status = CustomerMembershipStatus.Pending,
            NameSnapshot = plan.Name,
            PriceSnapshot = plan.Price,
            CurrencySnapshot = plan.Currency,
            BillingIntervalSnapshot = plan.BillingInterval,
            RolloverPolicySnapshot = plan.RolloverPolicy,
            CancellationPolicySnapshot = plan.CancellationPolicy,
            AllowFreezeSnapshot = plan.AllowFreeze,
            PastDueGraceDaysSnapshot = plan.PastDueGraceDays,
            EnrolledBy = $"customer:{customerId}",
            CreatedAt = now,
        };
        db.Add(membership);
        foreach (var line in plan.Benefits.OrderBy(x => x.SortOrder))
        {
            db.Add(new CustomerMembershipBenefit
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerMembershipId = membership.Id,
                ServiceOfferingId = line.ServiceOfferingId,
                ServiceName = line.ServiceName,
                QuantityPerCycle = line.QuantityPerCycle,
                SortOrder = line.SortOrder,
            });
        }

        db.Add(NewOperation(tenantId, keyHash, fingerprint, customerAuth0Sub, membership.Id, now));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race on the idempotency key (replay) or on the one-live-membership-per-plan index.
            db.ChangeTracker.Clear();
            var winner = await ReplayAsync(tenantId, keyHash, fingerprint, cancellationToken);
            return winner ?? Result.Failure<MembershipSubscribeResponse>(RetentionErrorCodes.MembershipAlreadyActiveError());
        }

        await AuditAsync($"customer:{customerId}", AuditActions.RetentionMembershipSubscribed, membership.Id, tenantId, cancellationToken);
        return await EnsureInitialCheckoutAsync(membership, cancellationToken);
    }

    // ---- PaymentCompleted / PaymentFailed ----

    public async Task<bool> ApplyPaymentCompletedAsync(
        Guid tenantId,
        Guid paymentIntentId,
        decimal amount,
        string currency,
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        var payment = await db.Set<MembershipPayment>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PaymentIntentId == paymentIntentId, cancellationToken);
        if (payment is null)
        {
            return false; // Not a membership payment.
        }

        if (payment.Status is MembershipPaymentStatus.Succeeded or MembershipPaymentStatus.Unapplied)
        {
            return true; // Duplicate delivery: the cycle was already applied (or flagged) — never twice.
        }

        var now = clock.GetUtcNow();
        if (amount != payment.Amount || !string.Equals(currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
        {
            // Never grant on a mismatched amount; flag for manual review instead of retrying forever.
            logger.LogWarning(
                "Membership payment mismatch payment={PaymentId} intent={PaymentIntentId}", payment.Id, paymentIntentId);
            return await MarkUnappliedAsync(payment, "amount_mismatch", now, cancellationToken);
        }

        var membership = await db.Set<CustomerMembership>()
            .Include(x => x.Benefits)
            .Include(x => x.Balances)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == payment.CustomerMembershipId, cancellationToken);
        if (membership is null)
        {
            return await MarkUnappliedAsync(payment, "membership_missing", now, cancellationToken);
        }

        payment.ProviderReference = Truncate(providerReference, 128);
        payment.Status = MembershipPaymentStatus.Succeeded;
        payment.FailureReason = null;
        payment.ResolvedAt = now;

        string auditAction;
        if (payment.Kind == MembershipPaymentKind.Initial)
        {
            if (membership.Status != CustomerMembershipStatus.Pending)
            {
                db.ChangeTracker.Clear();
                return await MarkUnappliedByIdAsync(tenantId, paymentIntentId, "membership_not_pending", now, cancellationToken);
            }

            membership.Activate(now);
            membership.ProviderName = provider.ProviderName;
            // References only. A live mandate / authorisation code is captured by Sprint 34 settlement work.
            if (provider.ProviderName == "Stub")
            {
                membership.ProviderMandateRef = $"stub_mandate_{membership.Id:N}";
                membership.ProviderSubscriptionRef = $"stub_sub_{membership.Id:N}";
            }

            MembershipCycles.GrantCurrentCycle(db, membership, SystemActor, null, now);
            events.Add(new CustomerMembershipActivated(membership.Id, tenantId, membership.TenantCustomerId, now));
            auditAction = AuditActions.RetentionMembershipActivated;
        }
        else
        {
            var renewable = membership.Status is CustomerMembershipStatus.Active or CustomerMembershipStatus.PastDue
                && payment.CycleNumber == membership.CurrentCycleNumber + 1;
            if (!renewable)
            {
                db.ChangeTracker.Clear();
                return await MarkUnappliedByIdAsync(tenantId, paymentIntentId, "membership_not_renewable", now, cancellationToken);
            }

            ApplyRenewal(membership, SystemActor, now);
            auditAction = AuditActions.RetentionMembershipRenewed;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent delivery applied this cycle first.
            db.ChangeTracker.Clear();
            events.TakeAll();
            return await db.Set<MembershipPayment>().AsNoTracking()
                .AnyAsync(
                    x => x.TenantId == tenantId && x.PaymentIntentId == paymentIntentId
                        && x.Status == MembershipPaymentStatus.Succeeded,
                    cancellationToken);
        }

        await AuditAsync(SystemActor, auditAction, membership.Id, tenantId, cancellationToken);
        return true;
    }

    public async Task<bool> ApplyPaymentFailedAsync(
        Guid tenantId, Guid paymentIntentId, string? reason, CancellationToken cancellationToken = default)
    {
        var payment = await db.Set<MembershipPayment>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PaymentIntentId == paymentIntentId, cancellationToken);
        if (payment is null)
        {
            return false;
        }

        if (payment.Status != MembershipPaymentStatus.Pending)
        {
            return true; // Already resolved (paid, flagged, or failure already recorded).
        }

        var now = clock.GetUtcNow();
        payment.Status = MembershipPaymentStatus.Failed;
        payment.FailureReason = Truncate(string.IsNullOrWhiteSpace(reason) ? "provider_failed" : reason, 200);
        payment.ResolvedAt = now;

        Guid? pastDueMembership = null;
        if (payment.Kind == MembershipPaymentKind.Renewal)
        {
            var membership = await db.Set<CustomerMembership>()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == payment.CustomerMembershipId, cancellationToken);
            if (membership is not null && membership.Status is CustomerMembershipStatus.Active or CustomerMembershipStatus.PastDue)
            {
                membership.FailedPaymentCount++;
                membership.MarkPastDue();
                events.Add(new CustomerMembershipPaymentFailed(
                    membership.Id, tenantId, membership.TenantCustomerId, "payment_failed", now));
                pastDueMembership = membership.Id;
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            events.TakeAll();
            return true;
        }

        if (pastDueMembership is { } id)
        {
            await AuditAsync(SystemActor, AuditActions.RetentionMembershipPaymentFailed, id, tenantId, cancellationToken);
        }

        return true;
    }

    // ---- Scheduled sweep ----

    public async Task<MembershipBillingRunResult> ProcessDueAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(batchSize, 1, 500);
        var cancellations = await CompleteCancellationsAsync(take, cancellationToken);
        var (renewals, pastDue, expired) = await SweepEndedCyclesAsync(take, cancellationToken);
        var reconciled = await ReconcilePendingAsync(take, cancellationToken);
        return new MembershipBillingRunResult(renewals, pastDue, expired, cancellations, reconciled);
    }

    private async Task<int> CompleteCancellationsAsync(int take, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var due = await db.Set<CustomerMembership>()
            .Include(x => x.Balances)
            .Where(x => x.Status == CustomerMembershipStatus.CancelPending && x.CurrentCycleEnd != null && x.CurrentCycleEnd <= now)
            .OrderBy(x => x.CurrentCycleEnd)
            .Take(take)
            .ToListAsync(cancellationToken);

        var completed = 0;
        foreach (var membership in due)
        {
            try
            {
                var closed = membership.CompleteCancellation(now);
                if (closed.IsFailure)
                {
                    continue;
                }

                var forfeited = await MembershipCycles.ReleaseAndForfeitAllAsync(
                    db, events, membership, SystemActor, "membership_cancelled", now, cancellationToken);
                if (forfeited.IsFailure)
                {
                    db.ChangeTracker.Clear();
                    events.TakeAll();
                    continue;
                }

                await db.SaveChangesAsync(cancellationToken);
                completed++;
                await AuditAsync(SystemActor, AuditActions.RetentionMembershipCancelled, membership.Id, membership.TenantId, cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // A booking raced the cycle end; leave it for the next run.
                db.ChangeTracker.Clear();
                events.TakeAll();
                logger.LogWarning(ex, "Membership cancellation skipped membership={MembershipId} (concurrent change)", membership.Id);
            }
        }

        return completed;
    }

    private async Task<(int Renewals, int PastDue, int Expired)> SweepEndedCyclesAsync(int take, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var due = await db.Set<CustomerMembership>()
            .Include(x => x.Balances)
            .Where(x => (x.Status == CustomerMembershipStatus.Active || x.Status == CustomerMembershipStatus.PastDue)
                && x.CurrentCycleEnd != null && x.CurrentCycleEnd <= now)
            .OrderBy(x => x.CurrentCycleEnd)
            .Take(take)
            .ToListAsync(cancellationToken);

        var billable = new Dictionary<Guid, bool>();
        int renewals = 0, pastDue = 0, expired = 0;
        foreach (var membership in due)
        {
            try
            {
                if (membership.Status == CustomerMembershipStatus.PastDue && membership.GraceElapsed(now))
                {
                    var closed = membership.Expire(now);
                    var forfeited = closed.IsSuccess
                        ? await MembershipCycles.ReleaseAndForfeitAllAsync(
                            db, events, membership, SystemActor, "membership_expired", now, cancellationToken)
                        : closed;
                    if (forfeited.IsFailure)
                    {
                        db.ChangeTracker.Clear();
                        events.TakeAll();
                        continue;
                    }

                    events.Add(new CustomerMembershipExpired(membership.Id, membership.TenantId, membership.TenantCustomerId, now));
                    await db.SaveChangesAsync(cancellationToken);
                    expired++;
                    await AuditAsync(SystemActor, AuditActions.RetentionMembershipExpired, membership.Id, membership.TenantId, cancellationToken);
                    continue;
                }

                // Renewal charge first (the orchestrator saves on the shared context), then the status change.
                var started = false;
                if (membership.IsOnline && OnlineBillingEnabled)
                {
                    if (!billable.TryGetValue(membership.TenantId, out var allowed))
                    {
                        allowed = await entitlements.IsRecurringBillingEntitledAsync(membership.TenantId, cancellationToken);
                        billable[membership.TenantId] = allowed;
                    }

                    if (allowed)
                    {
                        var intent = await EnsureRenewalPaymentAsync(membership, now, cancellationToken);
                        if (intent.IsFailure)
                        {
                            logger.LogWarning(
                                "Membership renewal charge could not start membership={MembershipId} code={Code}",
                                membership.Id, intent.Error.Code);
                        }
                        else
                        {
                            started = intent.Value;
                        }
                    }
                }

                if (started)
                {
                    renewals++;
                }

                if (membership.MarkPastDue())
                {
                    events.Add(new CustomerMembershipPaymentFailed(
                        membership.Id, membership.TenantId, membership.TenantCustomerId, "renewal_unpaid", now));
                    await db.SaveChangesAsync(cancellationToken);
                    pastDue++;
                    await AuditAsync(SystemActor, AuditActions.RetentionMembershipPastDue, membership.Id, membership.TenantId, cancellationToken);
                }
                else if (started)
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
            catch (DbUpdateException ex)
            {
                db.ChangeTracker.Clear();
                events.TakeAll();
                logger.LogWarning(ex, "Membership billing sweep skipped membership={MembershipId} (concurrent change)", membership.Id);
            }
        }

        return (renewals, pastDue, expired);
    }

    /// <summary>Safety net: applies confirmed payments whose <c>PaymentCompleted</c> delivery failed or never arrived.</summary>
    public async Task<int> ReconcilePendingAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        // Give the normal PaymentCompleted path a head start before second-guessing it.
        var cutoff = clock.GetUtcNow().AddMinutes(-2);
        var pending = await db.Set<MembershipPayment>().AsNoTracking()
            .Where(x => x.Status == MembershipPaymentStatus.Pending && x.CreatedAt <= cutoff)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new { x.TenantId, x.PaymentIntentId })
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);

        var applied = 0;
        foreach (var row in pending)
        {
            var intent = await payments.GetAsync(row.PaymentIntentId, cancellationToken);
            if (intent.IsFailure || intent.Value!.TenantId != row.TenantId)
            {
                continue;
            }

            if (string.Equals(intent.Value.Status, "completed", StringComparison.OrdinalIgnoreCase)
                && await ApplyPaymentCompletedAsync(
                    row.TenantId, row.PaymentIntentId, intent.Value.Amount, intent.Value.Currency,
                    intent.Value.ProviderReference, cancellationToken))
            {
                applied++;
            }
            else if (string.Equals(intent.Value.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                await ApplyPaymentFailedAsync(row.TenantId, row.PaymentIntentId, "provider_failed", cancellationToken);
            }
        }

        return applied;
    }

    // ---- Internals ----

    /// <summary>Closes the ended cycle (forfeit / roll over) and starts the next one with its credits.</summary>
    private void ApplyRenewal(CustomerMembership membership, string actor, DateTimeOffset now)
    {
        var carry = MembershipCycles.CloseCurrentCycle(db, membership, actor, now);
        membership.ApplyRenewal(now);
        MembershipCycles.GrantCurrentCycle(db, membership, actor, null, now, carry);
        events.Add(new CustomerMembershipRenewed(
            membership.Id, membership.TenantId, membership.TenantCustomerId, membership.CurrentCycleNumber, now));
    }

    private async Task<Result<bool>> EnsureRenewalPaymentAsync(
        CustomerMembership membership, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var nextCycle = membership.CurrentCycleNumber + 1;
        var attempts = await db.Set<MembershipPayment>().AsNoTracking()
            .Where(x => x.TenantId == membership.TenantId && x.CustomerMembershipId == membership.Id && x.CycleNumber == nextCycle)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Status, x.CreatedAt })
            .ToListAsync(cancellationToken);
        if (attempts.Any(x => x.Status is MembershipPaymentStatus.Pending or MembershipPaymentStatus.Succeeded)
            || attempts.Count >= MaxRenewalAttempts
            || (attempts.Count > 0 && now - attempts[0].CreatedAt < RenewalRetryInterval))
        {
            return Result.Success(false);
        }

        var intent = await payments.InitializeAsync(
            new InitializePaymentRequest(
                membership.TenantId,
                null,
                membership.PriceSnapshot,
                membership.CurrencySnapshot,
                "link",
                $"Membership renewal: {membership.NameSnapshot}"),
            $"retention:membership:{membership.Id:N}:cycle:{nextCycle}:try:{attempts.Count + 1}",
            cancellationToken);
        if (intent.IsFailure)
        {
            return Result.Failure<bool>(intent.Error);
        }

        db.Add(NewPayment(membership, MembershipPaymentKind.Renewal, nextCycle, intent.Value!.Id, now));
        return Result.Success(true);
    }

    private async Task<Result<MembershipSubscribeResponse>> EnsureInitialCheckoutAsync(
        CustomerMembership membership, CancellationToken cancellationToken)
    {
        var rows = await db.Set<MembershipPayment>().AsNoTracking()
            .Where(x => x.TenantId == membership.TenantId && x.CustomerMembershipId == membership.Id
                && x.Kind == MembershipPaymentKind.Initial)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        if (membership.Status == CustomerMembershipStatus.Active)
        {
            return Result.Success(new MembershipSubscribeResponse(
                membership.Id,
                rows.FirstOrDefault(x => x.Status == MembershipPaymentStatus.Succeeded)?.PaymentIntentId ?? Guid.Empty,
                "active",
                null));
        }

        if (membership.Status != CustomerMembershipStatus.Pending)
        {
            return Result.Failure<MembershipSubscribeResponse>(RetentionErrorCodes.MembershipInvalidStateError());
        }

        if (rows.FirstOrDefault(x => x.Status == MembershipPaymentStatus.Pending) is { } open)
        {
            var current = await payments.GetAsync(open.PaymentIntentId, cancellationToken);
            return current.IsFailure
                ? Result.Failure<MembershipSubscribeResponse>(current.Error)
                : Result.Success(new MembershipSubscribeResponse(
                    membership.Id, open.PaymentIntentId, "pending", EmptyToNull(current.Value!.CheckoutUrl)));
        }

        // Deterministic key: retrying after a failed init returns the same intent, never a second charge.
        var intent = await payments.InitializeAsync(
            new InitializePaymentRequest(
                membership.TenantId,
                null,
                membership.PriceSnapshot,
                membership.CurrencySnapshot,
                "link",
                $"Membership: {membership.NameSnapshot}"),
            $"retention:membership:{membership.Id:N}:cycle:1:try:{rows.Count + 1}",
            cancellationToken);
        if (intent.IsFailure)
        {
            return Result.Failure<MembershipSubscribeResponse>(intent.Error);
        }

        db.Add(NewPayment(membership, MembershipPaymentKind.Initial, 1, intent.Value!.Id, clock.GetUtcNow()));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Result.Failure<MembershipSubscribeResponse>(RetentionErrorCodes.BenefitConflictError());
        }

        return Result.Success(new MembershipSubscribeResponse(
            membership.Id, intent.Value.Id, "pending", EmptyToNull(intent.Value.CheckoutUrl)));
    }

    private async Task<bool> MarkUnappliedByIdAsync(
        Guid tenantId, Guid paymentIntentId, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var payment = await db.Set<MembershipPayment>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PaymentIntentId == paymentIntentId, cancellationToken);
        return payment is null || await MarkUnappliedAsync(payment, reason, now, cancellationToken);
    }

    private async Task<bool> MarkUnappliedAsync(
        MembershipPayment payment, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        payment.Status = MembershipPaymentStatus.Unapplied;
        payment.FailureReason = reason;
        payment.ResolvedAt = now;
        logger.LogWarning(
            "Membership payment not applied payment={PaymentId} membership={MembershipId} reason={Reason}",
            payment.Id, payment.CustomerMembershipId, reason);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
        }

        return true;
    }

    private static MembershipPayment NewPayment(
        CustomerMembership membership, MembershipPaymentKind kind, int cycle, Guid intentId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = membership.TenantId,
            CustomerMembershipId = membership.Id,
            CycleNumber = cycle,
            Kind = kind,
            PaymentIntentId = intentId,
            Amount = membership.PriceSnapshot,
            Currency = membership.CurrencySnapshot,
            Status = MembershipPaymentStatus.Pending,
            CreatedAt = now,
        };

    private static BenefitOperation NewOperation(
        Guid tenantId, string keyHash, string fingerprint, string customerAuth0Sub, Guid membershipId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            KeyHash = keyHash,
            Fingerprint = fingerprint,
            Kind = SubscribeKind,
            Actor = $"customer:{customerAuth0Sub}",
            ResultCustomerMembershipId = membershipId,
            OccurredAt = now,
        };

    private async Task<Result<MembershipSubscribeResponse>?> ReplayAsync(
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
            return Result.Failure<MembershipSubscribeResponse>(RetentionErrorCodes.IdempotencyConflictError());
        }

        var membership = await db.Set<CustomerMembership>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == operation.ResultCustomerMembershipId, cancellationToken);
        return membership is null
            ? Result.Failure<MembershipSubscribeResponse>(RetentionErrorCodes.MembershipNotFoundError())
            : await EnsureInitialCheckoutAsync(membership, cancellationToken);
    }

    private Task AuditAsync(string actor, string action, Guid id, Guid tenantId, CancellationToken cancellationToken) =>
        audit?.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actor,
                action,
                "retention.customer_membership",
                id.ToString(),
                correlation?.CorrelationId ?? "background",
                clock.GetUtcNow(),
                $"{{\"tenantId\":\"{tenantId}\"}}"),
            cancellationToken) ?? Task.CompletedTask;

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

/// <summary>
/// Retention's <c>PaymentCompleted</c> / <c>PaymentFailed</c> subscriber for memberships. Runs in its own DI
/// scope/DbContext (domain event handlers for one event execute concurrently). Failures never fail payment
/// confirmation; the billing job's reconcile pass retries.
/// </summary>
public sealed class MembershipPaymentHandler(
    IServiceScopeFactory scopes,
    ILogger<MembershipPaymentHandler> logger)
    : IDomainEventHandler<Adeni.Domain.Finance.Events.PaymentCompleted>,
      IDomainEventHandler<Adeni.Domain.Finance.Events.PaymentFailed>
{
    public async Task HandleAsync(Adeni.Domain.Finance.Events.PaymentCompleted domainEvent, CancellationToken cancellationToken = default)
    {
        if (domainEvent.BookingId.HasValue)
        {
            return; // Membership payments are never booking-linked.
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var coordinator = scope.ServiceProvider.GetRequiredService<IMembershipBillingCoordinator>();
            await coordinator.ApplyPaymentCompletedAsync(
                domainEvent.TenantId, domainEvent.PaymentIntentId, domainEvent.Amount, domainEvent.Currency,
                domainEvent.ProviderReference, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Membership payment apply failed for intent={PaymentIntentId}", domainEvent.PaymentIntentId);
        }
    }

    public async Task HandleAsync(Adeni.Domain.Finance.Events.PaymentFailed domainEvent, CancellationToken cancellationToken = default)
    {
        if (domainEvent.BookingId.HasValue)
        {
            return;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var coordinator = scope.ServiceProvider.GetRequiredService<IMembershipBillingCoordinator>();
            await coordinator.ApplyPaymentFailedAsync(
                domainEvent.TenantId, domainEvent.PaymentIntentId, domainEvent.Reason, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Membership payment failure handling failed for intent={PaymentIntentId}", domainEvent.PaymentIntentId);
        }
    }
}

public sealed class MembershipBillingHangfireJob(IMembershipBillingCoordinator billing)
{
    public const string RecurringJobId = "retention-membership-billing-v1";

    [Hangfire.DisableConcurrentExecution(300)]
    public async Task ExecuteAsync(CancellationToken cancellationToken) =>
        await billing.ProcessDueAsync(200, cancellationToken);
}
