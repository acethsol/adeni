namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Retention;
using Adeni.Domain.Retention;
using Adeni.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class LoyaltyExpiryService(
    AdeniDbContext db,
    TimeProvider clock,
    ILogger<LoyaltyExpiryService> logger) : ILoyaltyExpiryService
{
    private const string SystemActor = "system:loyalty-expiry";

    public async Task<int> ExpireDuePointsAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var programs = await db.Set<LoyaltyProgram>().AsNoTracking()
            .Where(x => x.IsEnabled && x.PointsExpiryMonths != null && x.PointsExpiryMonths > 0)
            .Select(x => new { x.TenantId, Months = x.PointsExpiryMonths!.Value })
            .ToListAsync(cancellationToken);
        if (programs.Count == 0)
        {
            return 0;
        }

        var programByTenant = programs.ToDictionary(x => x.TenantId, x => x.Months);
        var tenantIds = programByTenant.Keys.ToArray();

        var accounts = await db.Set<LoyaltyAccount>()
            .Where(x => tenantIds.Contains(x.TenantId) && x.PointsBalance > 0)
            .OrderBy(x => x.UpdatedAt)
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);

        var expiredAccounts = 0;
        foreach (var account in accounts)
        {
            if (!programByTenant.TryGetValue(account.TenantId, out var months))
            {
                continue;
            }

            try
            {
                var ledger = await db.Set<LoyaltyTransaction>()
                    .AsNoTracking()
                    .Where(x => x.TenantId == account.TenantId && x.LoyaltyAccountId == account.Id)
                    .ToListAsync(cancellationToken);

                var toExpire = LoyaltyFifo.ExpiredPointsNotYetWritten(ledger, months, now);
                if (toExpire <= 0)
                {
                    continue;
                }

                var tracked = await db.Set<LoyaltyAccount>()
                    .FirstAsync(x => x.TenantId == account.TenantId && x.Id == account.Id, cancellationToken);
                if (tracked.PointsBalance < toExpire)
                {
                    toExpire = tracked.PointsBalance;
                }

                if (toExpire <= 0)
                {
                    continue;
                }

                var row = tracked.RecordExpire(toExpire, SystemActor, now);
                if (row.IsFailure)
                {
                    continue;
                }

                db.Add(row.Value!);
                await db.SaveChangesAsync(cancellationToken);
                expiredAccounts++;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                db.ChangeTracker.Clear();
                logger.LogWarning(ex, "Loyalty expiry skipped account={AccountId}", account.Id);
                break;
            }
        }

        return expiredAccounts;
    }
}

public sealed class LoyaltyExpiryHangfireJob(ILoyaltyExpiryService expiry)
{
    public const string RecurringJobId = "retention-loyalty-expiry-v1";

    [DisableConcurrentExecution(300)]
    public Task ExecuteAsync(CancellationToken cancellationToken) =>
        expiry.ExpireDuePointsAsync(200, cancellationToken);
}
