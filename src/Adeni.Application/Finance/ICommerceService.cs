namespace Adeni.Application.Finance;

using Adeni.Domain.Common;
using Adeni.Domain.Finance;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCommerceRequest(decimal Amount, string Currency, string Label, bool GiftCardPurchase = false,
    bool AllowsGiftCard = false, DateTimeOffset? ExpiresAt = null);
public sealed record CommerceRequestView(Guid Id, string Purpose, decimal Amount, string Currency, string Label,
    bool AllowsGiftCard, string State, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? PaidAt, string ShareUrl, string? Tender = null)
{
    public override string ToString() => $"Payment request {Id} {State} [credential redacted]";
}
public sealed record PublicCommerceView(string BusinessName, string Label, decimal Amount, string Currency, string State,
    bool AllowsGiftCard, bool RequiresCustomerLogin);
public sealed record CommerceCheckoutView(string State, string? CheckoutUrl)
{
    public override string ToString() => $"Checkout {State} [redirect redacted]";
}
public sealed record GiftCardView(Guid Id, string BusinessName, decimal OriginalAmount, decimal RemainingAmount, string Currency,
    string State, string CodeSuffix, DateTimeOffset? IssuedAt);
public sealed record GiftTransactionView(Guid Id, string Kind, decimal Delta, decimal BalanceAfter, DateTimeOffset OccurredAt);
public sealed record GiftCodeView(string Code)
{
    public override string ToString() => "Gift code [redacted]";
}
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record RedeemCommerceRequest(string Code)
{
    public override string ToString() => "Gift redemption [redacted]";
}
public sealed record CommercePrivacyExport(IReadOnlyList<GiftCardView> PurchasedCards);

public interface IFinanceCommerceAccessQuery
{
    Task<bool> CanReadAsync(Guid tenantId, string actor, CancellationToken ct);
    Task<bool> CanManageAsync(Guid tenantId, string actor, CancellationToken ct);
    Task<Guid?> CustomerIdAsync(string actor, CancellationToken ct);
}

public interface ICommerceService
{
    Task<Result<CommerceRequestView>> CreateAsync(Guid tenant, string actor, string? key, CreateCommerceRequest body, CancellationToken ct);
    Task<Result<IReadOnlyList<CommerceRequestView>>> ListAsync(Guid tenant, string actor, int page, CancellationToken ct);
    Task<Result<CommerceRequestView>> CancelAsync(Guid tenant, string actor, Guid requestId, string? key, CancellationToken ct);
    Task<Result<PublicCommerceView>> ResolveAsync(string token, CancellationToken ct);
    Task<Result<CommerceCheckoutView>> CheckoutAsync(string token, string? actor, string? key, CancellationToken ct);
    Task<Result<PublicCommerceView>> RedeemAsync(string token, string? key, string code, CancellationToken ct);
    Task<Result<IReadOnlyList<GiftCardView>>> CardsAsync(Guid tenant, string actor, int page, CancellationToken ct);
    Task<Result<IReadOnlyList<GiftCardView>>> PurchasedCardsAsync(string actor, CancellationToken ct);
    Task<Result<GiftCodeView>> RevealAsync(string actor, Guid cardId, CancellationToken ct);
    Task<Result<IReadOnlyList<GiftTransactionView>>> TransactionsAsync(Guid tenant, string actor, Guid cardId, CancellationToken ct);
    Task<Result<CommerceRequestView>> RefundAsync(Guid tenant, string actor, Guid requestId, string? key, CancellationToken ct);
    Task<Result<CommerceRequestView>> ReverseAsync(Guid tenant, string actor, Guid requestId, string? key, CancellationToken ct);
}

// The existing payment orchestrator remains the only provider/webhook entry point.
public interface ICommercePaymentLifecycle
{
    Task<Result<CommerceCheckoutView>> InitializeCommerceAsync(Guid attemptId, CancellationToken ct);
    Task<Result<ProviderRefundResult>> RefundCommerceAsync(Guid attemptId, CancellationToken ct);
}
public interface ICommercePaymentCompletion
{
    Task<bool> OwnsIntentAsync(Guid intentId, CancellationToken ct);
    Task<Result<PaymentWebhookResult>> CompleteAsync(PaymentIntentRecord intent, PaymentWebhookPayload payload, CancellationToken ct);
}
public interface ICommercePrivacyService
{
    Task StageErasureAsync(Guid customerId, CancellationToken ct);
    Task<CommercePrivacyExport> ExportAsync(Guid customerId, CancellationToken ct);
}

public static class CommerceErrors
{
    public static Error Invalid => new(ErrorCodes.CommerceValidation, "Invalid commerce request.");
    public static Error Conflict => new(ErrorCodes.CommerceConflict, "The request cannot be processed in its current state.");
    public static Error Unavailable => new(ErrorCodes.CommerceSettlementRequired, "Business settlement is not available for this payment.");
    public static Error Credential => new(ErrorCodes.CommerceCredentialInvalid, "The payment request or gift credential is unavailable.");
}
