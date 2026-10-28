namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Events;
using Adeni.Application.Retention;
using Adeni.Domain.Auditing;
using Adeni.Domain.Retention;
using Adeni.Domain.Retention.Events;
using Adeni.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>Forfeits unused credits on Active packages past <c>ExpiresAt</c> (releases open reservations first).</summary>
public sealed class PackageExpiryService(
    AdeniDbContext db,
    IDomainEventCollector events,
    TimeProvider clock,
    ILogger<PackageExpiryService> logger,
    IAuditLogWriter? audit = null) : IPackageExpiryService
{
    private const string SystemActor = "system:retention-expiry";

    public async Task<int> ExpireDuePackagesAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var due = await db.Set<CustomerPackage>()
            .Include(x => x.Balances)
            .Where(x => x.Status == CustomerPackageStatus.Active && x.ExpiresAt != null && x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt)
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var package in due)
        {
            try
            {
                foreach (var balance in package.Balances)
                {
                    foreach (var open in await RetentionLedger.OpenReservationsAsync(db, package.TenantId, balance.Id, cancellationToken))
                    {
                        var released = balance.Release(open.Quantity, open.BookingId, open.LocationId, SystemActor, "package_expired", now);
                        if (released.IsSuccess)
                        {
                            db.Add(released.Value!);
                            events.Add(new BenefitReleased(balance.Id, package.TenantId, open.BookingId, open.Quantity, now));
                        }
                    }

                    if (balance.ForfeitRemaining(BenefitTransactionKind.Forfeit, SystemActor, "package_expired", now) is { } forfeit)
                    {
                        db.Add(forfeit);
                    }
                }

                package.Status = CustomerPackageStatus.Expired;
                package.ExpiredAt = now;
                events.Add(new CustomerPackageExpired(package.Id, package.TenantId, now));
                await db.SaveChangesAsync(cancellationToken);
                expired++;

                if (audit is not null)
                {
                    await audit.WriteAsync(
                        new AuditEntry(
                            Guid.NewGuid(), SystemActor, AuditActions.RetentionPackageExpired,
                            "retention.customer_package", package.Id.ToString(), "background", now,
                            $"{{\"tenantId\":\"{package.TenantId}\"}}"),
                        cancellationToken);
                }
            }
            catch (DbUpdateException ex)
            {
                // A booking raced the expiry; leave this package for the next run.
                db.ChangeTracker.Clear();
                events.TakeAll();
                logger.LogWarning(ex, "Package expiry skipped package={PackageId} (concurrent change)", package.Id);
                break;
            }
        }

        return expired;
    }
}

public sealed class PackageExpiryHangfireJob(IPackageExpiryService expiry, IPackagePurchaseActivation purchases)
{
    public const string RecurringJobId = "retention-package-expiry-v1";

    [DisableConcurrentExecution(300)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await expiry.ExpireDuePackagesAsync(200, cancellationToken);
        // Safety net for online purchases whose PaymentCompleted delivery failed after the payment was confirmed.
        await purchases.ReconcilePendingAsync(100, cancellationToken);
    }
}