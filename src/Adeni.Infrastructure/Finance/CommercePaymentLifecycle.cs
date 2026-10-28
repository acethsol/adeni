namespace Adeni.Infrastructure.Finance;

using Adeni.Application.Finance;
using Adeni.Domain.Common;
using Microsoft.EntityFrameworkCore;

public sealed partial class PaymentOrchestrator : ICommercePaymentLifecycle
{
    private async Task<Result<PaymentWebhookResult>> CompleteCommerceAsync(Adeni.Domain.Finance.PaymentIntentRecord intent, PaymentWebhookPayload payload, CancellationToken ct)
    {
        var result = await commerce!.CompleteAsync(intent, payload, ct);
        if (result.IsSuccess && result.Value!.Message == "Payment settled.")
        {
            domainEventCollector.Add(new Adeni.Domain.Finance.Events.PaymentCompleted(intent.Id, intent.TenantId, null,
                intent.Type, intent.Amount, intent.Currency, intent.ProviderReference, intent.UpdatedAt));
            await dbContext.SaveChangesAsync(ct);
            await tenantRealtime.PublishPaymentCompletedAsync(intent.TenantId,intent.Id,null,intent.ProviderReference,ct);
            await WriteAuditAsync(Guid.NewGuid(),intent.TenantId,"commerce.payment_confirmed","payment_intent",intent.Id,ct);
        }
        return result;
    }

    public async Task<Result<ProviderRefundResult>> RefundCommerceAsync(Guid attemptId, CancellationToken ct)
    {
        var attempt = await dbContext.Set<Adeni.Domain.Finance.CommercePaymentAttempt>().SingleAsync(x => x.Id == attemptId, ct);
        if (attempt.Provider != "Stub" || paymentProvider.ProviderName != "Stub") return Result.Failure<ProviderRefundResult>(CommerceErrors.Unavailable);
        var intent = await dbContext.PaymentIntents.SingleAsync(x => x.Id == attempt.PaymentIntentId, ct);
        return await paymentProvider.RefundAsync(intent.ProviderReference, intent.Amount, ct);
    }

    public async Task<Result<CommerceCheckoutView>> InitializeCommerceAsync(Guid attemptId, CancellationToken ct)
    {
        var attempt = await dbContext.Set<Adeni.Domain.Finance.CommercePaymentAttempt>().SingleOrDefaultAsync(x => x.Id == attemptId, ct);
        if (attempt is null) return Result.Failure<CommerceCheckoutView>(CommerceErrors.Credential);
        // This slice intentionally cannot initialize real money without merchant settlement.
        if (attempt.Provider != "Stub" || paymentProvider.ProviderName != "Stub") return Result.Failure<CommerceCheckoutView>(CommerceErrors.Unavailable);
        if (attempt.CheckoutUrl is not null) return Result.Success(new CommerceCheckoutView(attempt.State, attempt.CheckoutUrl));
        var intent = await dbContext.PaymentIntents.SingleAsync(x => x.Id == attempt.PaymentIntentId && x.TenantId == attempt.TenantId, ct);
        var response = await paymentProvider.InitializeAsync(new(intent.Id, intent.TenantId, null, intent.Amount, intent.Currency,
            intent.Type, intent.ProviderReference, intent.Description, null, null, 0m), ct);
        if (response.IsFailure) return Result.Failure<CommerceCheckoutView>(response.Error);
        attempt.CheckoutUrl = response.Value!.CheckoutUrl; attempt.State = "pending"; attempt.UpdatedAt = DateTimeOffset.UtcNow;
        try { await dbContext.SaveChangesAsync(ct); }
        catch (DbUpdateException) { dbContext.ChangeTracker.Clear(); return Result.Failure<CommerceCheckoutView>(CommerceErrors.Conflict); }
        return Result.Success(new CommerceCheckoutView("PaymentPending", attempt.CheckoutUrl));
    }
}
