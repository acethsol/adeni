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

public sealed class CustomerPackageService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    IDomainEventCollector events,
    TimeProvider clock,
    IAuditLogWriter? audit = null,
    ICorrelationContext? correlation = null) : ICustomerPackageService
{
    private const string SellKind = "sell";
    private const string VoidKind = "void";

    public async Task<Result<CustomerPackageListResponse>> ListForCustomerAsync(
        Guid tenantId, Guid tenantCustomerId, CancellationToken cancellationToken = default)
    {
        var exists = await db.TenantCustomers.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == tenantCustomerId, cancellationToken);
        if (!exists)
        {
            return Result.Failure<CustomerPackageListResponse>(Error.NotFound("Customer"));
        }

        var rows = await db.Set<CustomerPackage>().AsNoTracking()
            .Include(x => x.Balances)
            .Where(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var now = clock.GetUtcNow();
        return Result.Success(new CustomerPackageListResponse(rows.Select(x => RetentionMapper.ToResponse(x, now)).ToArray()));
    }

    public async Task<Result<CustomerPackageResponse>> GetAsync(
        Guid tenantId, Guid customerPackageId, CancellationToken cancellationToken = default)
    {
        var row = await db.Set<CustomerPackage>().AsNoTracking()
            .Include(x => x.Balances)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == customerPackageId, cancellationToken);
        return row is null
            ? Result.Failure<CustomerPackageResponse>(RetentionErrorCodes.PackageNotFoundError())
            : Result.Success(RetentionMapper.ToResponse(row, clock.GetUtcNow()));
    }

    public async Task<Result<CustomerPackageLedgerResponse>> GetLedgerAsync(
        Guid tenantId, Guid customerPackageId, CancellationToken cancellationToken = default)
    {
        var exists = await db.Set<CustomerPackage>().AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == customerPackageId, cancellationToken);
        if (!exists)
        {
            return Result.Failure<CustomerPackageLedgerResponse>(RetentionErrorCodes.PackageNotFoundError());
        }

        var balanceIds = await db.Set<BenefitBalance>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerPackageId == customerPackageId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var rows = await db.Set<BenefitTransaction>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && balanceIds.Contains(x.BenefitBalanceId))
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Sequence)
            .ToListAsync(cancellationToken);

        return Result.Success(new CustomerPackageLedgerResponse(rows
            .Select(x => new BenefitTransactionResponse(
                x.Id, x.Sequence, x.Kind.ToString().ToLowerInvariant(), x.Quantity, x.Delta, x.ReservedAfter,
                x.UsedAfter, x.RemainingAfter, x.BenefitBalanceId, x.BookingId, x.BusinessLocationId, x.Reason, x.OccurredAt))
            .ToArray()));
    }

    public async Task<Result<CustomerPackageResponse>> SellAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid tenantCustomerId,
        SellPackageRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsurePackagesEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<CustomerPackageResponse>(entitled.Error);
        }

        var key = Adeni.Application.Common.IdempotencyKeyNormalizer.Normalize(idempotencyKey);
        if (key is null)
        {
            return Result.Failure<CustomerPackageResponse>(RetentionErrorCodes.IdempotencyKeyRequiredError());
        }

        var method = request.PaymentMethod?.Trim().ToLowerInvariant();
        var channel = method switch
        {
            SellPaymentMethods.Till => PackageSoldChannel.InPersonTill,
            SellPaymentMethods.Transfer => PackageSoldChannel.InPersonTransfer,
            _ => (PackageSoldChannel?)null,
        };
        if (channel is null)
        {
            return Result.Failure<CustomerPackageResponse>(
                RetentionErrorCodes.ValidationError("Payment method must be till or transfer."));
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > 500 })
        {
            return Result.Failure<CustomerPackageResponse>(RetentionErrorCodes.ValidationError("Note must be at most 500 characters."));
        }

        var keyHash = RetentionHash.KeyHash(tenantId, SellKind, actor.Id, key);
        var fingerprint = RetentionHash.Fingerprint(
            request.PackageDefinitionId, tenantCustomerId, request.BusinessLocationId, method, note);

        var replay = await ReplayAsync(tenantId, keyHash, fingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay.Value;
        }

        var customerExists = await db.TenantCustomers.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == tenantCustomerId, cancellationToken);
        if (!customerExists)
        {
            return Result.Failure<CustomerPackageResponse>(Error.NotFound("Customer"));
        }

        var definition = await db.Set<PackageDefinition>().AsNoTracking()
            .Include(x => x.Benefits)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.PackageDefinitionId, cancellationToken);
        if (definition is null)
        {
            return Result.Failure<CustomerPackageResponse>(RetentionErrorCodes.PackageNotFoundError());
        }

        if (!definition.IsActive)
        {
            return Result.Failure<CustomerPackageResponse>(RetentionErrorCodes.PackageInactiveError());
        }

        var location = await TenantLocationScope.ValidateOptionalLocationIdAsync(
            db, tenantId, request.BusinessLocationId, cancellationToken);
        if (location.IsFailure || location.Value is null)
        {
            return Result.Failure<CustomerPackageResponse>(
                location.IsFailure ? location.Error : RetentionErrorCodes.ValidationError("A sale location is required."));
        }

        if (!PackageScopePolicy.CanSellAt(
                definition.SellScope, LocationIdList.Parse(definition.SellLocationIdsJson), location.Value))
        {
            return Result.Failure<CustomerPackageResponse>(RetentionErrorCodes.SellLocationDeniedError());
        }

        var now = clock.GetUtcNow();
        var package = new CustomerPackage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenantCustomerId = tenantCustomerId,
            PackageDefinitionId = definition.Id,
            SoldAtLocationId = location.Value,
            SoldChannel = channel.Value,
            Status = CustomerPackageStatus.Active,
            NameSnapshot = definition.Name,
            ListPriceSnapshot = definition.ListPrice,
            CurrencySnapshot = definition.Currency,
            RedemptionScopeSnapshot = definition.RedemptionScope,
            RedemptionLocationIdsJson = definition.RedemptionLocationIdsJson,
            Note = note,
            SoldBy = actor.Id,
            CreatedAt = now,
            ActivatedAt = now,
            ExpiresAt = now.AddMonths(definition.EffectiveExpiryMonths),
        };
        db.Add(package);

        foreach (var benefit in definition.Benefits.OrderBy(x => x.SortOrder))
        {
            var balance = BenefitBalance.Grant(
                tenantId, package.Id, benefit.ServiceOfferingId, benefit.ServiceName, benefit.QuantityGranted,
                actor.Id, location.Value, now, out var grant);
            package.Balances.Add(balance);
            db.Add(grant);
        }

        db.Add(new BenefitOperation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            KeyHash = keyHash,
            Fingerprint = fingerprint,
            Kind = SellKind,
            Actor = actor.Id,
            ResultCustomerPackageId = package.Id,
            OccurredAt = now,
        });
        events.Add(new CustomerPackageActivated(package.Id, tenantId, tenantCustomerId, now));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race on the idempotency key: surface the winner's result.
            db.ChangeTracker.Clear();
            events.TakeAll();
            var winner = await ReplayAsync(tenantId, keyHash, fingerprint, cancellationToken);
            return winner ?? Result.Failure<CustomerPackageResponse>(RetentionErrorCodes.BenefitConflictError());
        }

        await AuditAsync(actor.Id, AuditActions.RetentionPackageSold, package.Id, tenantId, cancellationToken);
        return Result.Success(RetentionMapper.ToResponse(package, now));
    }

    public async Task<Result<CustomerPackageResponse>> VoidAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid customerPackageId,
        VoidCustomerPackageRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var entitled = await entitlements.EnsurePackagesEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<CustomerPackageResponse>(entitled.Error);
        }

        if (!actor.IsOwner)
        {
            return Result.Failure<CustomerPackageResponse>(RetentionErrorCodes.OwnerRequiredError());
        }

        var key = Adeni.Application.Common.IdempotencyKeyNormalizer.Normalize(idempotencyKey);
        if (key is null)
        {
            return Result.Failure<CustomerPackageResponse>(RetentionErrorCodes.IdempotencyKeyRequiredError());
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason is { Length: > 500 })
        {
            return Result.Failure<CustomerPackageResponse>(RetentionErrorCodes.ValidationError("Reason must be at most 500 characters."));
        }

        var keyHash = RetentionHash.KeyHash(tenantId, VoidKind, actor.Id, key);
        var fingerprint = RetentionHash.Fingerprint(customerPackageId, reason);
        var replay = await ReplayAsync(tenantId, keyHash, fingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay.Value;
        }

        var package = await db.Set<CustomerPackage>()
            .Include(x => x.Balances)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == customerPackageId, cancellationToken);
        if (package is null)
        {
            return Result.Failure<CustomerPackageResponse>(RetentionErrorCodes.PackageNotFoundError());
        }

        if (package.Status != CustomerPackageStatus.Active)
        {
            return Result.Failure<CustomerPackageResponse>(RetentionErrorCodes.PackageNotActiveError());
        }

        var now = clock.GetUtcNow();
        foreach (var balance in package.Balances)
        {
            foreach (var open in await RetentionLedger.OpenReservationsAsync(db, tenantId, balance.Id, cancellationToken))
            {
                var released = balance.Release(open.Quantity, open.BookingId, open.LocationId, actor.Id, "package_voided", now);
                if (released.IsFailure)
                {
                    return Result.Failure<CustomerPackageResponse>(released.Error);
                }

                db.Add(released.Value!);
                events.Add(new BenefitReleased(balance.Id, tenantId, open.BookingId, open.Quantity, now));
            }

            if (balance.ForfeitRemaining(BenefitTransactionKind.Void, actor.Id, reason, now) is { } voided)
            {
                db.Add(voided);
            }
        }

        package.Status = CustomerPackageStatus.Voided;
        package.VoidedAt = now;
        package.VoidedBy = actor.Id;
        package.VoidReason = reason;

        db.Add(new BenefitOperation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            KeyHash = keyHash,
            Fingerprint = fingerprint,
            Kind = VoidKind,
            Actor = actor.Id,
            ResultCustomerPackageId = package.Id,
            OccurredAt = now,
        });
        events.Add(new CustomerPackageVoided(package.Id, tenantId, now));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            events.TakeAll();
            var winner = await ReplayAsync(tenantId, keyHash, fingerprint, cancellationToken);
            return winner ?? Result.Failure<CustomerPackageResponse>(RetentionErrorCodes.BenefitConflictError());
        }

        await AuditAsync(actor.Id, AuditActions.RetentionPackageVoided, package.Id, tenantId, cancellationToken);
        return Result.Success(RetentionMapper.ToResponse(package, now));
    }

    public async Task<Result<CustomerPackageListResponse>> ListForPlatformCustomerAsync(
        string customerAuth0Sub, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<CustomerPackageListResponse>(ErrorCodes.CustomerAuthRequiredError());
        }

        var customerId = await db.Customers.AsNoTracking()
            .Where(x => x.Auth0Sub == customerAuth0Sub)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (customerId is null)
        {
            return Result.Success(new CustomerPackageListResponse([]));
        }

        // Strictly the signed-in customer's own roster links; never trusts a client-supplied tenant.
        var links = await db.TenantCustomers.AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .Select(x => new { x.TenantId, x.Id })
            .ToListAsync(cancellationToken);
        if (links.Count == 0)
        {
            return Result.Success(new CustomerPackageListResponse([]));
        }

        var tenantCustomerIds = links.Select(x => x.Id).ToArray();
        var rows = await db.Set<CustomerPackage>().AsNoTracking()
            .Include(x => x.Balances)
            .Where(x => tenantCustomerIds.Contains(x.TenantCustomerId) && x.Status != CustomerPackageStatus.PendingPayment)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var tenantIds = rows.Select(x => x.TenantId).Distinct().ToArray();
        var names = await db.Tenants.AsNoTracking()
            .Where(x => tenantIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        // Public booking slug (primary active location) so clients can deep-link "book again" without a second lookup.
        var slugRows = await db.BusinessLocations.AsNoTracking()
            .Where(x => tenantIds.Contains(x.TenantId) && x.IsActive)
            .Select(x => new { x.TenantId, x.Slug, x.IsPrimary })
            .ToListAsync(cancellationToken);
        var slugs = slugRows
            .GroupBy(x => x.TenantId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IsPrimary).Select(x => x.Slug).First());

        var now = clock.GetUtcNow();
        return Result.Success(new CustomerPackageListResponse(rows
            .Select(x => RetentionMapper.ToResponse(x, now, names.GetValueOrDefault(x.TenantId), slugs.GetValueOrDefault(x.TenantId)))
            .ToArray()));
    }

    private async Task<Result<CustomerPackageResponse>?> ReplayAsync(
        Guid tenantId, string keyHash, string fingerprint, CancellationToken cancellationToken)
    {
        var operation = await db.Set<BenefitOperation>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.KeyHash == keyHash, cancellationToken);
        if (operation is null)
        {
            return null;
        }

        if (operation.Fingerprint != fingerprint || operation.ResultCustomerPackageId is null)
        {
            return Result.Failure<CustomerPackageResponse>(RetentionErrorCodes.IdempotencyConflictError());
        }

        return await GetAsync(tenantId, operation.ResultCustomerPackageId.Value, cancellationToken);
    }

    private Task AuditAsync(string actorId, string action, Guid id, Guid tenantId, CancellationToken cancellationToken) =>
        audit?.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actorId,
                action,
                "retention.customer_package",
                id.ToString(),
                correlation?.CorrelationId ?? "background",
                clock.GetUtcNow(),
                $"{{\"tenantId\":\"{tenantId}\"}}"),
            cancellationToken) ?? Task.CompletedTask;
}