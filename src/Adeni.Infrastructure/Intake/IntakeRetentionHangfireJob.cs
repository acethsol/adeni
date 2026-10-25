namespace Adeni.Infrastructure.Intake;

using Adeni.Application.Intake;
using Adeni.Domain.Intake;
using Adeni.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class IntakeRetentionHangfireJob(AdeniDbContext db, IIntakeRetentionService retention, ILogger<IntakeRetentionHangfireJob> logger)
{
    [DisableConcurrentExecution(3600)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var tenants = await db.Set<BookingIntakeAssignment>().AsNoTracking().Select(x => x.TenantId).Distinct().ToArrayAsync(cancellationToken);
        foreach (var tenant in tenants)
        {
            var result = await retention.PurgeTenantAsync(tenant, cancellationToken);
            logger.LogInformation("Intake retention tenant={TenantId} purged={Purged} removed={Removed} held={Held}",
                tenant, result.SubmissionsPurged, result.AssignmentsRemoved, result.Held);
        }
    }
}
