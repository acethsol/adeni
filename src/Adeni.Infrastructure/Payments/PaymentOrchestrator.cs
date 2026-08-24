namespace Adeni.Infrastructure.Payments;

using Adeni.Application.Abstractions;
using Adeni.Application.Events;
using Adeni.Application.Payments;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Payments;
using Adeni.Domain.Payments.Events;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class PaymentOrchestrator(
    AdeniDbContext dbContext,
    IPaymentProvider paymentProvider,
    IPlatformFeeCalculator platformFeeCalculator,
    IAuditLogWriter auditLogWriter,
    ICorrelationContext correlationContext,
    IDomainEventCollector domainEventCollector,
    IOptions<PaymentsOptions> paymentsOptions) : IPaymentOrchestrator
{
    public Task<Result<PaymentIntentResponse>> InitializeAsync(
        InitializePaymentRequest request,
        CancellationToken cancellationToken = default) =>
        CreatePaymentIntentAsync(
            request.TenantId,
            request.BookingId,
            request.Amount,
            request.Currency,
            ParsePaymentType(request.Type),
            request.Description,
            request.CustomerEmail,
            request.CallbackUrl,
            cancellationToken);

    public Task<Result<PaymentIntentResponse>> CreatePaymentLinkAsync(
        CreatePaymentLinkRequest request,
        CancellationToken cancellationToken = default) =>
        CreatePaymentIntentAsync(
            request.TenantId,
            bookingId: null,
            request.Amount,
            request.Currency,
            PaymentIntentType.Link,
            request.Description,
            request.CustomerEmail,
            request.CallbackUrl,
            cancellationToken);

    public async Task<Result<PaymentIntentResponse>> GetAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PaymentIntents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentIntentId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure<PaymentIntentResponse>(ErrorCodes.PaymentNotFoundError());
        }

        return Result.Success(Map(entity));
    }

    public async Task<Result<IReadOnlyList<PaymentLedgerEntry>>> ListLedgerAsync(
        Guid tenantId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var items = await dbContext.PaymentIntents
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PaymentLedgerEntry>>(
            items.Select(MapLedger).ToArray());
    }

    public async Task<Result<PaymentWebhookResult>> ProcessWebhookAsync(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        await WriteAuditAsync(
            Guid.NewGuid(),
            Guid.Empty,
            "payment.webhook_received",
            "payment_intent",
            null,
            cancellationToken);

        var parsed = await paymentProvider.ParseWebhookAsync(rawBody, headers, cancellationToken);
        if (parsed.IsFailure)
        {
            return Result.Failure<PaymentWebhookResult>(parsed.Error);
        }

        var payload = parsed.Value!;
        if (string.IsNullOrWhiteSpace(payload.ProviderReference))
        {
            return Result.Success(new PaymentWebhookResult(false, "Missing provider reference."));
        }

        var entity = await dbContext.PaymentIntents
            .FirstOrDefaultAsync(x => x.ProviderReference == payload.ProviderReference, cancellationToken);

        if (entity is null)
        {
            return Result.Success(new PaymentWebhookResult(false, "Payment intent not found."));
        }

        if (payload.IsRefund)
        {
            return await ApplyRefundFromWebhookAsync(entity, cancellationToken);
        }

        if (payload.IsSuccessful)
        {
            return await ApplySuccessAsync(entity, cancellationToken);
        }

        if (payload.IsFailed)
        {
            return await ApplyFailureAsync(entity, payload.FailureReason, cancellationToken);
        }

        return Result.Success(new PaymentWebhookResult(true, "Event ignored."));
    }

    public async Task<Result<PaymentIntentResponse>> ConfirmStubCheckoutAsync(
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PaymentIntents
            .FirstOrDefaultAsync(x => x.ProviderReference == providerReference, cancellationToken);

        if (entity is null)
        {
            return Result.Failure<PaymentIntentResponse>(ErrorCodes.PaymentNotFoundError());
        }

        if (entity.Status == PaymentIntentStatus.Completed)
        {
            return Result.Success(Map(entity));
        }

        var webhookResult = await ApplySuccessAsync(entity, cancellationToken);
        if (webhookResult.IsFailure)
        {
            return Result.Failure<PaymentIntentResponse>(webhookResult.Error);
        }

        return Result.Success(Map(entity));
    }

    public async Task<Result<PaymentIntentResponse>> InitiateRefundAsync(
        RefundPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PaymentIntents
            .FirstOrDefaultAsync(
                x => x.Id == request.PaymentIntentId && x.TenantId == request.TenantId,
                cancellationToken);

        if (entity is null)
        {
            return Result.Failure<PaymentIntentResponse>(ErrorCodes.PaymentNotFoundError());
        }

        if (entity.Status != PaymentIntentStatus.Completed)
        {
            return Result.Failure<PaymentIntentResponse>(ErrorCodes.PaymentRefundNotAllowedError());
        }

        entity.Status = PaymentIntentStatus.RefundPending;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var refundResult = await paymentProvider.RefundAsync(
            entity.ProviderReference,
            request.Amount,
            cancellationToken);

        if (refundResult.IsFailure)
        {
            entity.Status = PaymentIntentStatus.Completed;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            return Result.Failure<PaymentIntentResponse>(refundResult.Error);
        }

        entity.Status = PaymentIntentStatus.Refunded;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        domainEventCollector.Add(new RefundInitiated(
            entity.Id,
            entity.TenantId,
            entity.BookingId,
            refundResult.Value!.Amount,
            entity.Currency,
            entity.ProviderReference,
            DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            entity.Id,
            entity.TenantId,
            "payment.refunded",
            "payment_intent",
            entity.Id,
            cancellationToken);

        return Result.Success(Map(entity));
    }

    private async Task<Result<PaymentIntentResponse>> CreatePaymentIntentAsync(
        Guid tenantId,
        Guid? bookingId,
        decimal? amount,
        string currency,
        PaymentIntentType type,
        string? description,
        string? customerEmail,
        string? callbackUrl,
        CancellationToken cancellationToken)
    {
        var resolvedAmountResult = await ResolveAmountAsync(tenantId, bookingId, amount, type, cancellationToken);
        if (resolvedAmountResult.IsFailure)
        {
            return Result.Failure<PaymentIntentResponse>(resolvedAmountResult.Error);
        }

        var resolvedAmount = resolvedAmountResult.Value.amount;
        var resolvedCurrency = string.IsNullOrWhiteSpace(currency)
            ? resolvedAmountResult.Value.currency
            : currency.Trim().ToUpperInvariant();

        if (resolvedAmount <= 0)
        {
            return Result.Failure<PaymentIntentResponse>(ErrorCodes.PaymentInvalidAmountError());
        }

        if (resolvedCurrency.Length != 3)
        {
            return Result.Failure<PaymentIntentResponse>(ErrorCodes.PaymentInvalidCurrencyError());
        }

        var marketId = await ResolveMarketIdAsync(tenantId, cancellationToken);
        var platformFee = platformFeeCalculator.CalculateFee(resolvedAmount, marketId);
        var now = DateTimeOffset.UtcNow;
        var paymentIntentId = Guid.NewGuid();
        var reference = $"{paymentProvider.ProviderName.ToLowerInvariant()}_{paymentIntentId:N}"[..32];

        var entity = new PaymentIntentRecord
        {
            Id = paymentIntentId,
            TenantId = tenantId,
            BookingId = bookingId,
            Type = type,
            Amount = resolvedAmount,
            PlatformFeeAmount = platformFee,
            Currency = resolvedCurrency,
            Status = PaymentIntentStatus.Pending,
            ProviderReference = reference,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail.Trim(),
            CallbackUrl = string.IsNullOrWhiteSpace(callbackUrl) ? null : callbackUrl.Trim(),
            IdempotencyKey = reference,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var providerRequest = new ProviderInitializeRequest(
            entity.Id,
            entity.TenantId,
            entity.BookingId,
            entity.Amount,
            entity.Currency,
            entity.Type,
            entity.ProviderReference,
            entity.Description,
            entity.CustomerEmail,
            entity.CallbackUrl ?? BuildReceiptUrl(entity.Id),
            entity.PlatformFeeAmount);

        var providerResult = await paymentProvider.InitializeAsync(providerRequest, cancellationToken);
        if (providerResult.IsFailure)
        {
            return Result.Failure<PaymentIntentResponse>(providerResult.Error);
        }

        entity.ProviderReference = providerResult.Value!.ProviderReference;
        entity.IdempotencyKey = entity.ProviderReference;

        dbContext.PaymentIntents.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            entity.Id,
            entity.TenantId,
            "payment.initialized",
            "payment_intent",
            entity.Id,
            cancellationToken);

        var response = Map(entity) with
        {
            CheckoutUrl = providerResult.Value.CheckoutUrl,
        };

        return Result.Success(response);
    }

    private async Task<Result<(decimal amount, string currency)>> ResolveAmountAsync(
        Guid tenantId,
        Guid? bookingId,
        decimal? amount,
        PaymentIntentType type,
        CancellationToken cancellationToken)
    {
        if (amount.HasValue && amount.Value > 0)
        {
            return Result.Success((amount.Value, "NGN"));
        }

        if (!bookingId.HasValue || type != PaymentIntentType.Deposit)
        {
            return Result.Failure<(decimal, string)>(ErrorCodes.PaymentInvalidAmountError());
        }

        var bookingRow = await (
            from booking in dbContext.Bookings.AsNoTracking()
            join service in dbContext.ServiceOfferings.AsNoTracking() on booking.ServiceOfferingId equals service.Id
            join profile in dbContext.BusinessProfiles.AsNoTracking() on booking.TenantId equals profile.TenantId
            where booking.Id == bookingId.Value && booking.TenantId == tenantId
            select new { booking, service, profile })
            .FirstOrDefaultAsync(cancellationToken);

        if (bookingRow is null)
        {
            return Result.Failure<(decimal, string)>(Error.NotFound("Booking"));
        }

        if (bookingRow.profile.DepositPercent <= 0)
        {
            return Result.Failure<(decimal, string)>(ErrorCodes.PaymentDepositNotConfiguredError());
        }

        var depositAmount = Math.Round(
            bookingRow.service.PriceAmount * bookingRow.profile.DepositPercent / 100m,
            2,
            MidpointRounding.AwayFromZero);

        return Result.Success((depositAmount, bookingRow.service.Currency));
    }

    private async Task<string> ResolveMarketIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var marketId = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsPrimary)
            .Select(x => x.MarketId)
            .FirstOrDefaultAsync(cancellationToken);

        return marketId ?? "lagos";
    }

    private async Task<Result<PaymentWebhookResult>> ApplySuccessAsync(
        PaymentIntentRecord entity,
        CancellationToken cancellationToken)
    {
        if (entity.Status == PaymentIntentStatus.Completed)
        {
            return Result.Success(new PaymentWebhookResult(true, "Already completed."));
        }

        if (entity.Status is PaymentIntentStatus.Refunded or PaymentIntentStatus.RefundPending)
        {
            return Result.Success(new PaymentWebhookResult(false, "Payment already refunded."));
        }

        entity.Status = PaymentIntentStatus.Completed;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        domainEventCollector.Add(new PaymentCompleted(
            entity.Id,
            entity.TenantId,
            entity.BookingId,
            entity.Type,
            entity.Amount,
            entity.Currency,
            entity.ProviderReference,
            DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            entity.Id,
            entity.TenantId,
            "payment.confirmed",
            "payment_intent",
            entity.Id,
            cancellationToken);

        return Result.Success(new PaymentWebhookResult(true, "Payment confirmed."));
    }

    private async Task<Result<PaymentWebhookResult>> ApplyFailureAsync(
        PaymentIntentRecord entity,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (entity.Status == PaymentIntentStatus.Completed)
        {
            return Result.Success(new PaymentWebhookResult(false, "Already completed."));
        }

        entity.Status = PaymentIntentStatus.Failed;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        domainEventCollector.Add(new PaymentFailed(
            entity.Id,
            entity.TenantId,
            entity.BookingId,
            entity.Type,
            reason,
            DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            entity.Id,
            entity.TenantId,
            "payment.failed",
            "payment_intent",
            entity.Id,
            cancellationToken);

        return Result.Success(new PaymentWebhookResult(true, "Payment failed."));
    }

    private async Task<Result<PaymentWebhookResult>> ApplyRefundFromWebhookAsync(
        PaymentIntentRecord entity,
        CancellationToken cancellationToken)
    {
        if (entity.Status == PaymentIntentStatus.Refunded)
        {
            return Result.Success(new PaymentWebhookResult(true, "Already refunded."));
        }

        entity.Status = PaymentIntentStatus.Refunded;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        domainEventCollector.Add(new RefundInitiated(
            entity.Id,
            entity.TenantId,
            entity.BookingId,
            entity.Amount,
            entity.Currency,
            entity.ProviderReference,
            DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            entity.Id,
            entity.TenantId,
            "payment.refunded",
            "payment_intent",
            entity.Id,
            cancellationToken);

        return Result.Success(new PaymentWebhookResult(true, "Refund processed."));
    }

    private string BuildReceiptUrl(Guid paymentIntentId)
    {
        var baseUrl = paymentsOptions.Value.ReceiptBaseUrl.TrimEnd('/');
        return $"{baseUrl}?id={paymentIntentId}";
    }

    private static PaymentIntentType ParsePaymentType(string? type) =>
        type?.Trim().ToLowerInvariant() switch
        {
            "link" => PaymentIntentType.Link,
            "invoice" => PaymentIntentType.Invoice,
            _ => PaymentIntentType.Deposit,
        };

    private async Task WriteAuditAsync(
        Guid auditId,
        Guid tenantId,
        string action,
        string entityType,
        Guid? entityId,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditEntry(
                auditId,
                tenantId == Guid.Empty ? "system" : tenantId.ToString(),
                action,
                entityType,
                entityId?.ToString() ?? string.Empty,
                correlationContext.CorrelationId,
                DateTimeOffset.UtcNow),
            cancellationToken);
    }

    private PaymentIntentResponse Map(PaymentIntentRecord entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.BookingId,
            entity.Type.ToString().ToLowerInvariant(),
            entity.Amount,
            entity.PlatformFeeAmount,
            entity.Currency,
            entity.Status.ToString().ToLowerInvariant(),
            BuildCheckoutUrl(entity),
            entity.ProviderReference,
            entity.Description,
            entity.CreatedAt);

    private string BuildCheckoutUrl(PaymentIntentRecord entity)
    {
        if (paymentProvider.ProviderName == "Stub")
        {
            return $"{paymentsOptions.Value.StubCheckoutBaseUrl.TrimEnd('/')}/{entity.ProviderReference}";
        }

        return string.Empty;
    }

    private static PaymentLedgerEntry MapLedger(PaymentIntentRecord entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.BookingId,
            entity.Type.ToString().ToLowerInvariant(),
            entity.Amount,
            entity.PlatformFeeAmount,
            entity.Currency,
            entity.Status.ToString().ToLowerInvariant(),
            entity.ProviderReference,
            entity.Description,
            entity.CreatedAt,
            entity.UpdatedAt);
}
