namespace Adeni.Infrastructure.Finance;

using Adeni.Application.Finance;
using Adeni.Domain.Common;
using Adeni.Domain.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

public sealed class CommercePaymentCompletion(AdeniDbContext db, TimeProvider clock, IHostEnvironment? environment = null) : ICommercePaymentCompletion
{
    public Task<bool> OwnsIntentAsync(Guid intentId, CancellationToken ct) => db.Set<CommercePaymentAttempt>().AnyAsync(x => x.PaymentIntentId == intentId, ct);

    public async Task<Result<PaymentWebhookResult>> CompleteAsync(PaymentIntentRecord intent, PaymentWebhookPayload payload, CancellationToken ct)
    {
        var attempt = await db.Set<CommercePaymentAttempt>().SingleAsync(x => x.TenantId == intent.TenantId && x.PaymentIntentId == intent.Id, ct);
        if (attempt.Provider != "Stub" || (environment is not null && !environment.IsDevelopment() && environment.EnvironmentName != "Testing"))
            return Result.Failure<PaymentWebhookResult>(CommerceErrors.Unavailable);
        var r = await db.Set<CommercePaymentRequest>().SingleAsync(x => x.TenantId == intent.TenantId && x.Id == attempt.RequestId, ct);
        if (payload.IsRefund)
        {
            // A provider-side refund/dispute cannot silently leave newly issued gift value spendable.
            var disputedCard = await db.Set<BusinessGiftCard>().SingleOrDefaultAsync(x => x.TenantId == r.TenantId && x.PurchaseRequestId == r.Id, ct);
            if (disputedCard is not null && disputedCard.State != GiftCardState.Voided) disputedCard.State = GiftCardState.Suspended;
            attempt.State = "reconciliation"; attempt.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return Result.Success(new PaymentWebhookResult(true, "Financial reconciliation required."));
        }
        if (!payload.IsSuccessful) return Result.Success(new PaymentWebhookResult(true, "Awaiting authoritative paid state."));
        if (payload.Amount != intent.Amount || !string.Equals(payload.Currency, intent.Currency, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<PaymentWebhookResult>(CommerceErrors.Invalid);
        if (r.State == PaymentRequestState.Paid && intent.Status == PaymentIntentStatus.Completed)
            return Result.Success(new PaymentWebhookResult(true, "Already settled."));
        if (r.State != PaymentRequestState.PaymentPending || intent.Status != PaymentIntentStatus.Pending || attempt.State == "reconciliation")
            return Result.Failure<PaymentWebhookResult>(CommerceErrors.Conflict);
        BusinessGiftCard? issueCard = null;
        if (r.Purpose == PaymentRequestPurpose.GiftCardPurchase)
        {
            issueCard = await db.Set<BusinessGiftCard>().SingleAsync(x => x.TenantId == r.TenantId && x.PurchaseRequestId == r.Id, ct);
            if (issueCard.State != GiftCardState.PendingPayment) return Result.Failure<PaymentWebhookResult>(CommerceErrors.Conflict);
        }
        var now = clock.GetUtcNow();
        intent.Status = PaymentIntentStatus.Completed; intent.UpdatedAt = now;
        r.State = PaymentRequestState.Paid; r.PaidAt = now; attempt.State = "completed"; attempt.UpdatedAt = now;
        db.Add(new CommerceSettlement { Id = Guid.NewGuid(), TenantId = r.TenantId, RequestId = r.Id, PaymentIntentId = intent.Id,
            Amount = r.Amount, Currency = r.Currency, SettledAt = now });
        if (issueCard is { } card)
        {
            card.RemainingAmount = card.OriginalAmount; card.IssuedAt = now; card.State = GiftCardState.Active; card.Sequence = 1;
            db.Add(new GiftCardTransaction { Id = Guid.NewGuid(), TenantId = r.TenantId, GiftCardId = card.Id, RequestId = r.Id,
                Sequence = 1, Kind = GiftTransactionKind.Issue, Delta = card.OriginalAmount, BalanceAfter = card.RemainingAmount, Actor = "provider", OccurredAt = now });
        }
        db.Add(new CommerceOperation { Id = Guid.NewGuid(), TenantId = r.TenantId, RequestId = r.Id,
            KeyHash = CommerceCredentials.Hash($"complete:{intent.Id:N}"), Fingerprint = "", Kind = "payment_confirmed", Actor = "provider", OccurredAt = now });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return Result.Failure<PaymentWebhookResult>(CommerceErrors.Conflict); }
        return Result.Success(new PaymentWebhookResult(true, "Payment settled."));
    }
}
