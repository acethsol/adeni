namespace Adeni.Infrastructure.Payments;

using Adeni.Application.Payments;
using Adeni.Domain.Common;
using Adeni.Domain.Payments;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class StubPaymentProvider(AdeniDbContext dbContext) : IPaymentProvider
{
    public async Task<Result<PaymentIntentResponse>> InitializeAsync(
        InitializePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            return Result.Failure<PaymentIntentResponse>(Error.Validation("Payment amount must be greater than zero."));
        }

        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3)
        {
            return Result.Failure<PaymentIntentResponse>(Error.Validation("Currency must be a 3-letter ISO code."));
        }

        var now = DateTimeOffset.UtcNow;
        var reference = $"stub_{Guid.NewGuid():N}"[..24];
        var entity = new PaymentIntentRecord
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            BookingId = request.BookingId,
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Status = PaymentIntentStatus.Pending,
            ProviderReference = reference,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.PaymentIntents.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(Map(entity));
    }

    public async Task<Result<PaymentIntentResponse>> GetAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PaymentIntents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentIntentId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure<PaymentIntentResponse>(Error.NotFound("Payment"));
        }

        return Result.Success(Map(entity));
    }

    private static PaymentIntentResponse Map(PaymentIntentRecord entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.BookingId,
            entity.Amount,
            entity.Currency,
            entity.Status.ToString().ToLowerInvariant(),
            $"/checkout/stub/{entity.ProviderReference}",
            entity.ProviderReference);
}
