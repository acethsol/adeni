namespace Adeni.Infrastructure.Finance;
using Adeni.Application.Finance;
using Adeni.Domain.Common;
using Adeni.Domain.Finance;
using Microsoft.EntityFrameworkCore;

public sealed partial class CommerceService
{
    public async Task<Result<CommerceRequestView>> RefundAsync(Guid tenant, string actor, Guid requestId, string? key, CancellationToken ct)
    {
        if (!await access.CanManageAsync(tenant, actor, ct)) return Result.Failure<CommerceRequestView>(Denied);
        if (!Sandbox) return Result.Failure<CommerceRequestView>(CommerceErrors.Unavailable);
        if (!KeyValid(key)) return Result.Failure<CommerceRequestView>(CommerceErrors.Invalid);
        var r = await db.Set<CommercePaymentRequest>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == requestId, ct);
        if (r is null) return Result.Failure<CommerceRequestView>(Error.NotFound("Payment request"));
        var hash = KeyHash(tenant, "refund", actor, key!); var fingerprint = Fingerprint(requestId);
        var replay = await Replay(hash, ct);
        if (replay is not null && replay.Fingerprint != fingerprint) return Result.Failure<CommerceRequestView>(CommerceErrors.Conflict);
        if (replay is not null && r.State == PaymentRequestState.Refunded) return Result.Success(Map(r));
        var settlement = await db.Set<CommerceSettlement>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.RequestId == requestId, ct);
        if (settlement?.PaymentIntentId is not Guid intentId || r.State is not (PaymentRequestState.Paid or PaymentRequestState.RefundPending))
            return Result.Failure<CommerceRequestView>(CommerceErrors.Conflict);
        var card = await db.Set<BusinessGiftCard>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.PurchaseRequestId == r.Id, ct);
        if (card is not null && (card.RemainingAmount != card.OriginalAmount || card.State is not (GiftCardState.Active or GiftCardState.Suspended)))
            return Result.Failure<CommerceRequestView>(CommerceErrors.Conflict);
        var intent = await db.PaymentIntents.SingleAsync(x => x.Id == intentId && x.TenantId == tenant, ct);
        var attempt = await db.Set<CommercePaymentAttempt>().SingleAsync(x => x.PaymentIntentId == intentId && x.TenantId == tenant, ct);
        if (replay is null)
        {
            r.State = PaymentRequestState.RefundPending; db.Entry(r).Property(x => x.State).IsModified = true; intent.Status = PaymentIntentStatus.RefundPending; intent.UpdatedAt = Now;
            if (card is not null) card.State = GiftCardState.Suspended;
            Record(r, hash, fingerprint, "refund_started", actor);
            if (!await Save(ct)) return Result.Failure<CommerceRequestView>(CommerceErrors.Conflict);
        }
        var refund = await payments.RefundCommerceAsync(attempt.Id, ct);
        if (refund.IsFailure) return Result.Failure<CommerceRequestView>(refund.Error);
        if (refund.Value!.Amount != r.Amount || refund.Value.Status is not ("processed" or "succeeded"))
            return Result.Success(Map(r)); // pending/ambiguous is durable, never guessed refunded.
        if (card is not null)
        {
            card.RemainingAmount = 0; card.State = GiftCardState.Voided; card.Sequence++;
            db.Add(new GiftCardTransaction { Id = Guid.NewGuid(), TenantId = tenant, GiftCardId = card.Id, RequestId = r.Id,
                Sequence = card.Sequence, Kind = GiftTransactionKind.Void, Delta = -card.OriginalAmount, BalanceAfter = 0, Actor = actor, OccurredAt = Now });
        }
        intent.Status = PaymentIntentStatus.Refunded; intent.UpdatedAt = Now; settlement.ReversedAt = Now; r.State = PaymentRequestState.Refunded;
        Record(r, CommerceCredentials.Hash($"refund-confirmed:{intent.Id:N}"), "", "refund_confirmed", "provider");
        return await Save(ct) ? Result.Success(Map(r)) : Result.Failure<CommerceRequestView>(CommerceErrors.Conflict);
    }
}
