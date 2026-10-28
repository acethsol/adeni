namespace Adeni.Infrastructure.Finance;
using Adeni.Application.Finance;
using Adeni.Domain.Finance;
using Adeni.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
public sealed class CommerceReconciliationJob(AdeniDbContext db, ICommercePaymentLifecycle payments, IBillingDocumentService documents,
    IHostEnvironment environment, ILogger<CommerceReconciliationJob> logger)
{
    [DisableConcurrentExecution(300)]
    public async Task ExecuteAsync(CancellationToken ct)
    {
        // No external live operations before the settlement prerequisite is implemented.
        if(!environment.IsDevelopment() && environment.EnvironmentName!="Testing")return;
        var attempts=await db.Set<CommercePaymentAttempt>().AsNoTracking().Where(x=>x.Provider=="Stub" && (x.State=="initializing" || (x.State=="completed" && !db.BillingDocuments.Any(d=>d.PaymentIntentId==x.PaymentIntentId))))
            .OrderBy(x=>x.UpdatedAt).Take(100).ToArrayAsync(ct);
        foreach(var attempt in attempts)
        {
            if(attempt.State=="initializing")await payments.InitializeCommerceAsync(attempt.Id,ct);
            else await documents.IssueReceiptForPaymentAsync(attempt.PaymentIntentId,ct);
        }
        var unresolved=await db.Set<CommercePaymentAttempt>().CountAsync(x=>x.State=="reconciliation",ct);
        logger.LogInformation("Commerce reconciliation examined={Examined} unresolved={Unresolved}",attempts.Length,unresolved);
    }
}
