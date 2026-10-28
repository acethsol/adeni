namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Events;
using Adeni.Application.Finance;
using Adeni.Application.Retention;
using Adeni.Domain.Finance.Events;
using Adeni.Domain.Retention;
using Adeni.Domain.Retention.Events;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Grants credits for an online package purchase. Idempotent (Murphy): a package that is no longer
/// <c>PendingPayment</c> is never granted twice, and the package row's rowversion makes a concurrent
/// duplicate delivery lose with a concurrency error instead of double-granting.
/// </summary>
public sealed class PackagePurchaseActivation(
    AdeniDbContext db,
    IDomainEventCollector events,
    TimeProvider clock,
    ILogger<PackagePurchaseActivation> logger,
    IPaymentOrchestrator? payments = null) : IPackagePurchaseActivation
{
    private const string SystemActor = "system:package-purchase";

    public async Task<bool> ActivateForPaymentAsync(
        Guid tenantId,
        Guid paymentIntentId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var package = await db.Set<CustomerPackage>()
            .Include(x => x.Balances)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PaymentIntentId == paymentIntentId, cancellationToken);
        if (package is null)
        {
            return false; // Not a package payment.
        }

        if (package.Status != CustomerPackageStatus.PendingPayment)
        {
            return true; // Already activated (or voided/expired): duplicate delivery is a no-op.
        }

        if (amount != package.ListPriceSnapshot
            || !string.Equals(currency, package.CurrencySnapshot, StringComparison.OrdinalIgnoreCase))
        {
            // Never grant on a mismatched amount; leave pending for manual review.
            logger.LogWarning(
                "Package payment mismatch package={PackageId} intent={PaymentIntentId}", package.Id, paymentIntentId);
            return false;
        }

        var definition = await db.Set<PackageDefinition>().AsNoTracking()
            .Include(x => x.Benefits)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == package.PackageDefinitionId, cancellationToken);
        if (definition is null || definition.Benefits.Count == 0)
        {
            logger.LogWarning("Package definition missing for package={PackageId}", package.Id);
            return false;
        }

        var now = clock.GetUtcNow();
        package.Status = CustomerPackageStatus.Active;
        package.ActivatedAt = now;
        package.ExpiresAt = now.AddMonths(definition.EffectiveExpiryMonths);

        foreach (var benefit in definition.Benefits.OrderBy(x => x.SortOrder))
        {
            var balance = BenefitBalance.Grant(
                tenantId, package.Id, benefit.ServiceOfferingId, benefit.ServiceName, benefit.QuantityGranted,
                SystemActor, package.SoldAtLocationId, now, out var grant);
            // Explicit Add: the package is already tracked, and a client-set Guid key reached via a
            // navigation would otherwise be treated as Modified (UPDATE of a missing row).
            db.Add(balance);
            db.Add(grant);
        }

        events.Add(new CustomerPackageActivated(package.Id, tenantId, package.TenantCustomerId, now));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // A concurrent delivery activated it first.
            db.ChangeTracker.Clear();
            events.TakeAll();
            return await db.Set<CustomerPackage>().AsNoTracking()
                .AnyAsync(x => x.TenantId == tenantId && x.Id == package.Id && x.Status == CustomerPackageStatus.Active, cancellationToken);
        }
    }

    public async Task<int> ReconcilePendingAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (payments is null)
        {
            return 0;
        }

        // Give the normal PaymentCompleted path a head start before second-guessing it.
        var cutoff = clock.GetUtcNow().AddMinutes(-2);
        var pending = await db.Set<CustomerPackage>().AsNoTracking()
            .Where(x => x.Status == CustomerPackageStatus.PendingPayment && x.PaymentIntentId != null && x.CreatedAt <= cutoff)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new { x.TenantId, IntentId = x.PaymentIntentId!.Value })
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);

        var activated = 0;
        foreach (var row in pending)
        {
            var intent = await payments.GetAsync(row.IntentId, cancellationToken);
            if (intent.IsFailure
                || intent.Value!.TenantId != row.TenantId
                || !string.Equals(intent.Value.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (await ActivateForPaymentAsync(row.TenantId, row.IntentId, intent.Value.Amount, intent.Value.Currency, cancellationToken))
            {
                activated++;
            }
        }

        return activated;
    }
}

/// <summary>
/// Retention's <c>PaymentCompleted</c> subscriber. Runs the grant in its own DI scope/DbContext because domain event
/// handlers for one event execute concurrently and must not share a DbContext with the Finance handlers.
/// Failures never fail payment confirmation; <see cref="IPackagePurchaseActivation.ReconcilePendingAsync"/> retries.
/// </summary>
public sealed class PackagePurchasePaymentHandler(
    IServiceScopeFactory scopes,
    ILogger<PackagePurchasePaymentHandler> logger) : IDomainEventHandler<PaymentCompleted>
{
    public async Task HandleAsync(PaymentCompleted domainEvent, CancellationToken cancellationToken = default)
    {
        if (domainEvent.BookingId.HasValue)
        {
            return; // Package payments are never booking-linked.
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var activation = scope.ServiceProvider.GetRequiredService<IPackagePurchaseActivation>();
            await activation.ActivateForPaymentAsync(
                domainEvent.TenantId, domainEvent.PaymentIntentId, domainEvent.Amount, domainEvent.Currency, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Package activation failed for payment intent={PaymentIntentId}", domainEvent.PaymentIntentId);
        }
    }
}
