namespace Adeni.Infrastructure.Payments;

using Adeni.Application.Payments;
using Adeni.Domain.Common;
using Adeni.Domain.Payments;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class StubPaymentProvider(
    AdeniDbContext dbContext,
    IOptions<PaymentsOptions> paymentsOptions) : IPaymentProvider
{
    public string ProviderName => "Stub";

    public async Task<Result<ProviderInitializeResult>> InitializeAsync(
        ProviderInitializeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            return Result.Failure<ProviderInitializeResult>(ErrorCodes.PaymentInvalidAmountError());
        }

        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3)
        {
            return Result.Failure<ProviderInitializeResult>(ErrorCodes.PaymentInvalidCurrencyError());
        }

        var checkoutUrl =
            $"{paymentsOptions.Value.StubCheckoutBaseUrl.TrimEnd('/')}/{request.ProviderReference}";

        return Result.Success(new ProviderInitializeResult(checkoutUrl, request.ProviderReference));
    }

    public async Task<Result<ProviderPaymentStatus>> GetProviderStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PaymentIntents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProviderReference == providerReference, cancellationToken);

        if (entity is null)
        {
            return Result.Failure<ProviderPaymentStatus>(ErrorCodes.PaymentNotFoundError());
        }

        return Result.Success(new ProviderPaymentStatus(
            providerReference,
            entity.Status == PaymentIntentStatus.Completed,
            entity.Status == PaymentIntentStatus.Failed,
            entity.Status == PaymentIntentStatus.Failed ? "Stub payment failed." : null));
    }

    public Task<Result<PaymentWebhookPayload>> ParseWebhookAsync(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        _ = rawBody;
        _ = headers;
        return Task.FromResult(Result.Failure<PaymentWebhookPayload>(ErrorCodes.PaymentWebhookInvalidError()));
    }

    public Task<Result<ProviderRefundResult>> RefundAsync(
        string providerReference,
        decimal? amount,
        CancellationToken cancellationToken = default)
    {
        _ = providerReference;
        return Task.FromResult(Result.Success(new ProviderRefundResult(
            providerReference,
            amount ?? 0m,
            "processed")));
    }
}
