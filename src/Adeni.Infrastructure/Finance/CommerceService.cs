namespace Adeni.Infrastructure.Finance;

using System.Globalization;
using System.Text.Json;
using Adeni.Application.Finance;
using Adeni.Domain.Common;
using Adeni.Domain.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

public sealed partial class CommerceService(AdeniDbContext db, IFinanceCommerceAccessQuery access, CommerceCredentials credentials,
    ICommercePaymentLifecycle payments, IPaymentProvider provider, IHostEnvironment environment, TimeProvider clock, Adeni.Application.Abstractions.IAuditLogWriter? audit = null,
    Adeni.Application.Abstractions.ICorrelationContext? correlation = null) : ICommerceService
{
    private DateTimeOffset Now => clock.GetUtcNow();
    // No live fallback to platform-only Stripe/Paystack charges. Sprint 34 must supply merchant settlement first.
    private bool Sandbox => (environment.IsDevelopment() || environment.EnvironmentName == "Testing") && provider.ProviderName == "Stub";
    private static Error Denied => Error.Forbidden("Finance access is required.");
    private static bool KeyValid(string? key) => key is { Length: > 0 and <= 128 } && key.All(c => c is >= '!' and <= '~');
    private static string KeyHash(Guid tenant, string scope, string actor, string key) => CommerceCredentials.Hash($"{tenant:N}:{scope}:{actor}:{key}");
    private static string Fingerprint(params object?[] data) => CommerceCredentials.Hash(JsonSerializer.Serialize(data));
    private CommerceRequestView Map(CommercePaymentRequest r) => new(r.Id, r.Purpose.ToString(), r.Amount, r.Currency, r.Label,
        r.AllowsGiftCard, State(r), r.CreatedAt, r.ExpiresAt, r.PaidAt, credentials.ShareUrl(r));
    private string State(CommercePaymentRequest r) => r.State == PaymentRequestState.Active && r.ExpiresAt <= Now ? "Expired" : r.State.ToString();
    private async Task<PublicCommerceView> Public(CommercePaymentRequest r, CancellationToken ct) => new(
        await db.Tenants.Where(x => x.Id == r.TenantId).Select(x => x.Name).SingleAsync(ct), r.Label, r.Amount, r.Currency, State(r),
        r.AllowsGiftCard, r.Purpose == PaymentRequestPurpose.GiftCardPurchase);
    private Task<CommercePaymentRequest?> Find(string token, CancellationToken ct)
    {
        var hash = CommerceCredentials.Hash(token.ToUpperInvariant());
        return db.Set<CommercePaymentRequest>().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
    }
    private void Record(CommercePaymentRequest r, string hash, string fingerprint, string kind, string actor) => db.Add(new CommerceOperation {
        Id = Guid.NewGuid(), TenantId = r.TenantId, RequestId = r.Id, KeyHash = hash, Fingerprint = fingerprint, Kind = kind, Actor = actor, OccurredAt = Now });
    private Task<CommerceOperation?> Replay(string hash, CancellationToken ct) => db.Set<CommerceOperation>().AsNoTracking().SingleOrDefaultAsync(x => x.KeyHash == hash, ct);
    private async Task<bool> Save(CancellationToken ct)
    {
        var operations = db.ChangeTracker.Entries<CommerceOperation>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return false; }
        foreach (var op in operations) await Audit(op.Actor, "commerce." + op.Kind, op.RequestId, ct);
        return true;
    }
    private Task Audit(string actor, string action, Guid id, CancellationToken ct) => audit?.WriteAsync(new(
        Guid.NewGuid(), actor, action, "commerce", id.ToString(), correlation?.CorrelationId ?? "background", Now), ct) ?? Task.CompletedTask;

    public async Task<Result<CommerceRequestView>> CreateAsync(Guid tenant, string actor, string? key, CreateCommerceRequest body, CancellationToken ct)
    {
        if (!await access.CanManageAsync(tenant, actor, ct)) return Result.Failure<CommerceRequestView>(Denied);
        if (!Sandbox) return Result.Failure<CommerceRequestView>(CommerceErrors.Unavailable);
        var currency = body.Currency?.Trim().ToUpperInvariant(); var label = body.Label?.Trim();
        if (!KeyValid(key) || body.Amount <= 0 || body.Amount > 9999999999.99m || decimal.Round(body.Amount, 2) != body.Amount
            || currency is not ("NGN" or "CAD" or "USD") || string.IsNullOrEmpty(label) || label.Length > 200
            || (body.GiftCardPurchase && body.AllowsGiftCard)) return Result.Failure<CommerceRequestView>(CommerceErrors.Invalid);
        var hash = KeyHash(tenant, "create", actor, key!);
        var fingerprint = Fingerprint(body.Amount.ToString("G29", CultureInfo.InvariantCulture), currency, label, body.GiftCardPurchase, body.AllowsGiftCard, body.ExpiresAt);
        var replay = await Replay(hash, ct);
        if (replay is not null) return replay.Fingerprint == fingerprint
            ? Result.Success(Map(await db.Set<CommercePaymentRequest>().SingleAsync(x => x.TenantId == tenant && x.Id == replay.RequestId, ct)))
            : Result.Failure<CommerceRequestView>(CommerceErrors.Conflict);
        var expires = body.ExpiresAt ?? Now.AddDays(7);
        if (expires <= Now || expires > Now.AddDays(7)) return Result.Failure<CommerceRequestView>(CommerceErrors.Invalid);
        var r = new CommercePaymentRequest { Id = Guid.NewGuid(), TenantId = tenant, Purpose = body.GiftCardPurchase ? PaymentRequestPurpose.GiftCardPurchase : PaymentRequestPurpose.CustomCharge,
            Amount = body.Amount, Currency = currency, Label = label, AllowsGiftCard = body.AllowsGiftCard, CreatedBy = actor,
            CreatedAt = Now, ExpiresAt = expires, Nonce = CommerceCredentials.NewNonce(), KeyVersion = credentials.KeyVersion };
        r.TokenHash = CommerceCredentials.Hash(credentials.LinkToken(r)); db.Add(r);
        if (body.GiftCardPurchase)
        {
            var card = new BusinessGiftCard { Id = Guid.NewGuid(), TenantId = tenant, PurchaseRequestId = r.Id, OriginalAmount = r.Amount,
                Currency = r.Currency, Nonce = CommerceCredentials.NewNonce(), KeyVersion = credentials.KeyVersion };
            var code = credentials.GiftCode(card); card.CodeHash = CommerceCredentials.Hash($"{tenant:N}:{code}"); card.CodeSuffix = code[^8..]; db.Add(card);
        }
        Record(r, hash, fingerprint, "created", actor);
        return await Save(ct) ? Result.Success(Map(r)) : Result.Failure<CommerceRequestView>(CommerceErrors.Conflict);
    }

    public async Task<Result<IReadOnlyList<CommerceRequestView>>> ListAsync(Guid tenant, string actor, int page, CancellationToken ct)
    {
        if (!await access.CanReadAsync(tenant, actor, ct)) return Result.Failure<IReadOnlyList<CommerceRequestView>>(Denied);
        if (page is < 1 or > 10000) return Result.Failure<IReadOnlyList<CommerceRequestView>>(CommerceErrors.Invalid);
        var rows = await db.Set<CommercePaymentRequest>().AsNoTracking().Where(x => x.TenantId == tenant).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * 25).Take(25).ToArrayAsync(ct);
        await Audit(actor,"commerce.requests_read",tenant,ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var tenders = await db.Set<CommerceSettlement>().AsNoTracking().Where(x => x.TenantId == tenant && ids.Contains(x.RequestId))
            .Select(x => new { x.RequestId, Gift = x.GiftTransactionId != null }).ToDictionaryAsync(x => x.RequestId, x => x.Gift ? "GiftCard" : "Provider", ct);
        return Result.Success<IReadOnlyList<CommerceRequestView>>(rows.Select(r => Map(r) with { Tender = tenders.GetValueOrDefault(r.Id) }).ToArray());
    }

    public async Task<Result<CommerceRequestView>> CancelAsync(Guid tenant, string actor, Guid requestId, string? key, CancellationToken ct)
    {
        if (!await access.CanManageAsync(tenant, actor, ct)) return Result.Failure<CommerceRequestView>(Denied);
        if (!KeyValid(key)) return Result.Failure<CommerceRequestView>(CommerceErrors.Invalid);
        var r = await db.Set<CommercePaymentRequest>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == requestId, ct);
        if (r is null) return Result.Failure<CommerceRequestView>(Error.NotFound("Payment request"));
        var hash = KeyHash(tenant, "cancel", actor, key!); var fingerprint = Fingerprint(requestId);
        var replay = await Replay(hash, ct);
        if (replay is not null) return replay.Fingerprint == fingerprint ? Result.Success(Map(r)) : Result.Failure<CommerceRequestView>(CommerceErrors.Conflict);
        if (r.State != PaymentRequestState.Active) return Result.Failure<CommerceRequestView>(CommerceErrors.Conflict);
        r.State = PaymentRequestState.Cancelled; r.CancelledAt = Now; Record(r, hash, fingerprint, "cancelled", actor);
        return await Save(ct) ? Result.Success(Map(r)) : Result.Failure<CommerceRequestView>(CommerceErrors.Conflict);
    }

    public async Task<Result<PublicCommerceView>> ResolveAsync(string token, CancellationToken ct)
    {
        if (!CommerceCredentials.Valid(token)) return Result.Failure<PublicCommerceView>(CommerceErrors.Credential);
        var r = await Find(token, ct);
        return r is null ? Result.Failure<PublicCommerceView>(CommerceErrors.Credential) : Result.Success(await Public(r, ct));
    }

    public async Task<Result<CommerceCheckoutView>> CheckoutAsync(string token, string? actor, string? key, CancellationToken ct)
    {
        if (!CommerceCredentials.Valid(token)) return Result.Failure<CommerceCheckoutView>(CommerceErrors.Credential);
        if (!KeyValid(key)) return Result.Failure<CommerceCheckoutView>(CommerceErrors.Invalid);
        var r = await Find(token, ct);
        if (r is null) return Result.Failure<CommerceCheckoutView>(CommerceErrors.Credential);
        if (!Sandbox) return Result.Failure<CommerceCheckoutView>(CommerceErrors.Unavailable);
        Guid? purchaser = null;
        if (r.Purpose == PaymentRequestPurpose.GiftCardPurchase)
        {
            purchaser = actor is null ? null : await access.CustomerIdAsync(actor, ct);
            if (purchaser is null) return Result.Failure<CommerceCheckoutView>(Error.Forbidden("Customer sign-in is required."));
        }
        var hash = KeyHash(r.TenantId, $"checkout:{r.Id:N}", "payer", key!); var fingerprint = Fingerprint(r.Id, purchaser);
        var replay = await Replay(hash, ct);
        if (replay is not null)
        {
            if (replay.Fingerprint != fingerprint) return Result.Failure<CommerceCheckoutView>(CommerceErrors.Conflict);
            var existing = await db.Set<CommercePaymentAttempt>().AsNoTracking().SingleAsync(x => x.TenantId == r.TenantId && x.RequestId == r.Id, ct);
            if (r.State == PaymentRequestState.Paid) return Result.Success(new CommerceCheckoutView("Paid", null));
            return await payments.InitializeCommerceAsync(existing.Id, ct);
        }
        if (r.State == PaymentRequestState.Paid) return Result.Success(new CommerceCheckoutView("Paid", null));
        if (r.State == PaymentRequestState.PaymentPending)
        {
            if (r.Purpose == PaymentRequestPurpose.GiftCardPurchase && !await db.Set<BusinessGiftCard>().AnyAsync(x => x.TenantId == r.TenantId && x.PurchaseRequestId == r.Id && x.PurchaserCustomerId == purchaser, ct))
                return Result.Failure<CommerceCheckoutView>(CommerceErrors.Conflict);
            var pending = await db.Set<CommercePaymentAttempt>().AsNoTracking().SingleAsync(x => x.TenantId == r.TenantId && x.RequestId == r.Id, ct);
            Record(r, hash, fingerprint, "checkout_resumed", "payer");
            if (!await Save(ct)) return Result.Failure<CommerceCheckoutView>(CommerceErrors.Conflict);
            return await payments.InitializeCommerceAsync(pending.Id, ct);
        }
        if (r.State != PaymentRequestState.Active || r.ExpiresAt <= Now) return Result.Failure<CommerceCheckoutView>(CommerceErrors.Conflict);
        r.State = PaymentRequestState.PaymentPending;
        if (purchaser is not null)
            (await db.Set<BusinessGiftCard>().SingleAsync(x => x.TenantId == r.TenantId && x.PurchaseRequestId == r.Id, ct)).PurchaserCustomerId = purchaser;
        var intent = new PaymentIntentRecord { Id = Guid.NewGuid(), TenantId = r.TenantId, Amount = r.Amount, Currency = r.Currency,
            Type = PaymentIntentType.Link, Description = r.Label, CreatedAt = Now, UpdatedAt = Now };
        intent.ProviderReference = $"stub_f3_{intent.Id:N}"; intent.IdempotencyKey = $"f3:{intent.Id:N}";
        var attempt = new CommercePaymentAttempt { Id = Guid.NewGuid(), TenantId = r.TenantId, RequestId = r.Id, PaymentIntentId = intent.Id,
            Provider = provider.ProviderName, MerchantReference = intent.ProviderReference, CreatedAt = Now, UpdatedAt = Now };
        db.Add(intent); db.Add(attempt); Record(r, hash, fingerprint, "checkout", "payer");
        if (!await Save(ct)) return Result.Failure<CommerceCheckoutView>(CommerceErrors.Conflict);
        return await payments.InitializeCommerceAsync(attempt.Id, ct);
    }

    public async Task<Result<PublicCommerceView>> RedeemAsync(string token, string? key, string code, CancellationToken ct)
    {
        if (!CommerceCredentials.Valid(token) || !CommerceCredentials.Valid(code)) return Result.Failure<PublicCommerceView>(CommerceErrors.Credential);
        if (!KeyValid(key)) return Result.Failure<PublicCommerceView>(CommerceErrors.Invalid);
        var r = await Find(token, ct);
        if (r is null) return Result.Failure<PublicCommerceView>(CommerceErrors.Credential);
        if (!Sandbox) return Result.Failure<PublicCommerceView>(CommerceErrors.Unavailable);
        var codeHash = CommerceCredentials.Hash($"{r.TenantId:N}:{code.ToUpperInvariant()}");
        var hash = KeyHash(r.TenantId, $"redeem:{r.Id:N}", "payer", key!); var fingerprint = Fingerprint(r.Id, codeHash);
        var replay = await Replay(hash, ct);
        if (replay is not null) return replay.Fingerprint == fingerprint ? Result.Success(await Public(r, ct)) : Result.Failure<PublicCommerceView>(CommerceErrors.Conflict);
        if (r.State != PaymentRequestState.Active || r.ExpiresAt <= Now || !r.AllowsGiftCard || r.Purpose != PaymentRequestPurpose.CustomCharge)
            return Result.Failure<PublicCommerceView>(CommerceErrors.Conflict);
        var card = await db.Set<BusinessGiftCard>().SingleOrDefaultAsync(x => x.TenantId == r.TenantId && x.CodeHash == codeHash, ct);
        if (card is null || card.State != GiftCardState.Active || card.Currency != r.Currency || card.RemainingAmount < r.Amount)
            return Result.Failure<PublicCommerceView>(CommerceErrors.Credential);
        card.RemainingAmount -= r.Amount; card.Sequence++;
        if (card.RemainingAmount == 0) card.State = GiftCardState.Exhausted;
        var tx = new GiftCardTransaction { Id = Guid.NewGuid(), TenantId = r.TenantId, GiftCardId = card.Id, RequestId = r.Id,
            Kind = GiftTransactionKind.Redeem, Delta = -r.Amount, BalanceAfter = card.RemainingAmount, Sequence = card.Sequence, Actor = "holder", OccurredAt = Now };
        db.Add(tx); db.Add(new CommerceSettlement { Id = Guid.NewGuid(), TenantId = r.TenantId, RequestId = r.Id, GiftTransactionId = tx.Id,
            Amount = r.Amount, Currency = r.Currency, SettledAt = Now });
        r.State = PaymentRequestState.Paid; r.PaidAt = Now; Record(r, hash, fingerprint, "redeemed", "holder");
        return await Save(ct) ? Result.Success(await Public(r, ct)) : Result.Failure<PublicCommerceView>(CommerceErrors.Conflict);
    }

    public async Task<Result<CommerceRequestView>> ReverseAsync(Guid tenant, string actor, Guid requestId, string? key, CancellationToken ct)
    {
        if (!await access.CanManageAsync(tenant, actor, ct)) return Result.Failure<CommerceRequestView>(Denied);
        if (!KeyValid(key)) return Result.Failure<CommerceRequestView>(CommerceErrors.Invalid);
        var r = await db.Set<CommercePaymentRequest>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == requestId, ct);
        if (r is null) return Result.Failure<CommerceRequestView>(Error.NotFound("Payment request"));
        if (!Sandbox) return Result.Failure<CommerceRequestView>(CommerceErrors.Unavailable);
        var hash = KeyHash(tenant, "reverse", actor, key!); var fingerprint = Fingerprint(requestId);
        var replay = await Replay(hash, ct);
        if (replay is not null) return replay.Fingerprint == fingerprint ? Result.Success(Map(r)) : Result.Failure<CommerceRequestView>(CommerceErrors.Conflict);
        var settlement = await db.Set<CommerceSettlement>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.RequestId == r.Id, ct);
        if (r.State != PaymentRequestState.Paid || settlement?.GiftTransactionId is not Guid original || settlement.ReversedAt != null)
            return Result.Failure<CommerceRequestView>(CommerceErrors.Conflict);
        var debit = await db.Set<GiftCardTransaction>().SingleAsync(x => x.TenantId == tenant && x.Id == original, ct);
        var card = await db.Set<BusinessGiftCard>().SingleAsync(x => x.TenantId == tenant && x.Id == debit.GiftCardId, ct);
        if (card.State is not (GiftCardState.Active or GiftCardState.Exhausted)) return Result.Failure<CommerceRequestView>(CommerceErrors.Conflict);
        card.RemainingAmount += settlement.Amount; card.Sequence++; card.State = GiftCardState.Active;
        db.Add(new GiftCardTransaction { Id = Guid.NewGuid(), TenantId = tenant, GiftCardId = card.Id, RequestId = r.Id, ReversesTransactionId = original,
            Kind = GiftTransactionKind.RedemptionReversal, Delta = settlement.Amount, BalanceAfter = card.RemainingAmount, Sequence = card.Sequence, Actor = actor, OccurredAt = Now });
        settlement.ReversedAt = Now; r.State = PaymentRequestState.Refunded; Record(r, hash, fingerprint, "redemption_reversed", actor);
        return await Save(ct) ? Result.Success(Map(r)) : Result.Failure<CommerceRequestView>(CommerceErrors.Conflict);
    }

    private async Task<GiftCardView[]> CardViews(IQueryable<BusinessGiftCard> query, CancellationToken ct) => await (
        from c in query join t in db.Tenants on c.TenantId equals t.Id orderby c.IssuedAt descending, c.Id
        select new GiftCardView(c.Id, t.Name, c.OriginalAmount, c.RemainingAmount, c.Currency, c.State.ToString(), c.CodeSuffix, c.IssuedAt,
            db.BusinessLocations.Where(l => l.TenantId == c.TenantId && l.IsActive).OrderByDescending(l => l.IsPrimary).Select(l => l.Slug).FirstOrDefault())).Take(100).ToArrayAsync(ct);
    public async Task<Result<IReadOnlyList<GiftCardView>>> CardsAsync(Guid tenant, string actor, int page, CancellationToken ct)
    {
        if (!await access.CanReadAsync(tenant, actor, ct)) return Result.Failure<IReadOnlyList<GiftCardView>>(Denied);
        if (page is < 1 or > 10000) return Result.Failure<IReadOnlyList<GiftCardView>>(CommerceErrors.Invalid);
        var cards = db.Set<BusinessGiftCard>().AsNoTracking().Where(x => x.TenantId == tenant).OrderByDescending(x => x.IssuedAt).ThenBy(x => x.Id).Skip((page - 1) * 25).Take(25);
        await Audit(actor,"commerce.cards_read",tenant,ct);
        return Result.Success<IReadOnlyList<GiftCardView>>(await CardViews(cards, ct));
    }
    public async Task<Result<IReadOnlyList<GiftCardView>>> PurchasedCardsAsync(string actor, CancellationToken ct)
    {
        var customer = await access.CustomerIdAsync(actor, ct);
        if (customer is null) return Result.Failure<IReadOnlyList<GiftCardView>>(Denied);
        return Result.Success<IReadOnlyList<GiftCardView>>(await CardViews(db.Set<BusinessGiftCard>().AsNoTracking().Where(x => x.PurchaserCustomerId == customer && x.IssuedAt != null), ct));
    }
    public async Task<Result<GiftCodeView>> RevealAsync(string actor, Guid cardId, CancellationToken ct)
    {
        var customer = await access.CustomerIdAsync(actor, ct);
        var card = customer is null ? null : await db.Set<BusinessGiftCard>().SingleOrDefaultAsync(x => x.Id == cardId && x.PurchaserCustomerId == customer && x.IssuedAt != null, ct);
        if (card is null) return Result.Failure<GiftCodeView>(Error.NotFound("Gift card"));
        var r = await db.Set<CommercePaymentRequest>().SingleAsync(x => x.TenantId == card.TenantId && x.Id == card.PurchaseRequestId, ct);
        Record(r, CommerceCredentials.Hash(Guid.NewGuid().ToString()), "", "code_revealed", actor);
        if (!await Save(ct)) return Result.Failure<GiftCodeView>(CommerceErrors.Conflict);
        return Result.Success(new GiftCodeView(credentials.GiftCode(card)));
    }
    public async Task<Result<IReadOnlyList<GiftTransactionView>>> TransactionsAsync(Guid tenant, string actor, Guid cardId, CancellationToken ct)
    {
        if (!await access.CanReadAsync(tenant, actor, ct)) return Result.Failure<IReadOnlyList<GiftTransactionView>>(Denied);
        if (!await db.Set<BusinessGiftCard>().AnyAsync(x => x.TenantId == tenant && x.Id == cardId, ct)) return Result.Failure<IReadOnlyList<GiftTransactionView>>(Error.NotFound("Gift card"));
        await Audit(actor,"commerce.transactions_read",cardId,ct);
        return Result.Success<IReadOnlyList<GiftTransactionView>>(await db.Set<GiftCardTransaction>().AsNoTracking().Where(x => x.TenantId == tenant && x.GiftCardId == cardId)
            .OrderByDescending(x => x.Sequence).Take(100).Select(x => new GiftTransactionView(x.Id, x.Kind.ToString(), x.Delta, x.BalanceAfter, x.OccurredAt)).ToArrayAsync(ct));
    }
}
