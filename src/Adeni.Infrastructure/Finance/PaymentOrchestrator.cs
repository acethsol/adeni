namespace Adeni.Infrastructure.Finance;

using Adeni.Application.Abstractions;
using Adeni.Application.Common;
using Adeni.Application.Events;
using Adeni.Application.Finance;
using Adeni.Application.Realtime;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Finance;
using Adeni.Domain.Finance.Events;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed partial class PaymentOrchestrator(
    AdeniDbContext dbContext,
    IPaymentProvider paymentProvider,
    IPlatformFeeCalculator platformFeeCalculator,
    IAuditLogWriter auditLogWriter,
    ICorrelationContext correlationContext,
    IDomainEventCollector domainEventCollector,
    IOptions<FinanceOptions> paymentsOptions,
    ITenantRealtimePublisher tenantRealtime,
    IPaymentCheckoutRealtimeTokenService checkoutRealtimeTokens,
    ICustomerPaymentAccessQuery customerPaymentAccess,
    IBookingDepositCalculator bookingDepositCalculator,
    IPaymentCallbackUrlValidator callbackUrlValidator,
    ICommercePaymentCompletion? commerce = null) : IPaymentOrchestrator
{
    public async Task<Result<PaymentIntentResponse>> InitializeCustomerBookingPaymentAsync(
        string customerAuth0Sub,
        InitializePaymentRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<PaymentIntentResponse>(ErrorCodes.AuthRequiredError());
        }

        var paymentType = ParsePaymentType(request.Type);
        if (paymentType != PaymentIntentType.Deposit)
        {
            return Result.Failure<PaymentIntentResponse>(
                Error.Validation("Customer checkout supports booking deposit payments only."));
        }

        if (request.BookingId is not Guid bookingId)
        {
            return Result.Failure<PaymentIntentResponse>(Error.Validation("Booking id is required."));
        }

        if (!await customerPaymentAccess.OwnsBookingAsync(customerAuth0Sub, bookingId, cancellationToken))
        {
            return Result.Failure<PaymentIntentResponse>(Error.Forbidden("You do not have access to this booking."));
        }

        var booking = await dbContext.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == bookingId, cancellationToken);
        if (booking is null)
        {
            return Result.Failure<PaymentIntentResponse>(Error.NotFound("Booking"));
        }

        if (request.TenantId != Guid.Empty && request.TenantId != booking.TenantId)
        {
            return Result.Failure<PaymentIntentResponse>(Error.Forbidden("Booking does not belong to the specified business."));
        }

        var depositResult = await bookingDepositCalculator.CalculateRequiredDepositAsync(bookingId, cancellationToken);
        if (depositResult.IsFailure)
        {
            return Result.Failure<PaymentIntentResponse>(depositResult.Error);
        }

        var deposit = depositResult.Value!;
        Guid? tipStaffId = request.TipStaffMemberId;
        if (tipStaffId is Guid staffId)
        {
            var tipCheck = await ValidateTipStaffMemberAsync(booking.TenantId, staffId, cancellationToken);
            if (tipCheck.IsFailure)
            {
                return Result.Failure<PaymentIntentResponse>(tipCheck.Error);
            }
        }

        if (request.TipAmount is > 0 && tipStaffId is null)
        {
            return Result.Failure<PaymentIntentResponse>(Error.Validation("Tip staff member is required when tipping."));
        }

        var customerId = await dbContext.Customers.AsNoTracking()
            .Where(c => c.Auth0Sub == customerAuth0Sub)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return await CreatePaymentIntentAsync(
            booking.TenantId,
            bookingId,
            deposit.Amount,
            deposit.Currency,
            PaymentIntentType.Deposit,
            request.Description,
            request.CustomerEmail,
            callbackUrl: null,
            idempotencyKey,
            request.TipAmount,
            tipStaffId,
            idempotencyCustomerId: customerId,
            useServerCallbackForCustomer: true,
            cancellationToken);
    }

    public Task<Result<PaymentIntentResponse>> InitializeAsync(
        InitializePaymentRequest request,
        string? idempotencyKey = null,
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
            idempotencyKey,
            request.TipAmount,
            request.TipStaffMemberId,
            cancellationToken: cancellationToken);

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
            idempotencyKey: null,
            tipAmount: null,
            tipStaffMemberId: null,
            cancellationToken: cancellationToken);

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

    public async Task<Result<PagedResult<PaymentLedgerEntry>>> ListLedgerAsync(
        Guid tenantId,
        int page = 1,
        int pageSize = 50,
        Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = Paging.Normalize(page, pageSize);
        if (normalized.IsFailure)
        {
            return Result.Failure<PagedResult<PaymentLedgerEntry>>(normalized.Error);
        }

        (page, pageSize) = normalized.Value!;

        var locationCheck = await TenantLocationScope.ValidateOptionalLocationIdAsync(
            dbContext,
            tenantId,
            locationId,
            cancellationToken);
        if (locationCheck.IsFailure)
        {
            return Result.Failure<PagedResult<PaymentLedgerEntry>>(locationCheck.Error!);
        }

        locationId = locationCheck.Value;

        var query = dbContext.PaymentIntents
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (locationId is Guid branchId)
        {
            query = query.Where(p =>
                p.BookingId != null
                && dbContext.Bookings.Any(b =>
                    b.TenantId == tenantId
                    && b.Id == p.BookingId
                    && b.BusinessLocationId == branchId));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<PaymentLedgerEntry>(
            items.Select(MapLedger).ToArray(),
            page,
            pageSize,
            totalCount));
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

        if (commerce is not null && await commerce.OwnsIntentAsync(entity.Id, cancellationToken))
            return await CompleteCommerceAsync(entity, payload, cancellationToken);

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

        var webhookResult = commerce is not null && await commerce.OwnsIntentAsync(entity.Id, cancellationToken)
            ? await CompleteCommerceAsync(entity, new("stub.paid", entity.ProviderReference, true, false, false, null, entity.Amount, entity.Currency), cancellationToken)
            : await ApplySuccessAsync(entity, cancellationToken);
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

        if (commerce is not null && await commerce.OwnsIntentAsync(entity.Id, cancellationToken))
            return Result.Failure<PaymentIntentResponse>(CommerceErrors.Conflict);

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

    public async Task<Result<PaymentReceiptResponse>> GetReceiptAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PaymentIntents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentIntentId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure<PaymentReceiptResponse>(ErrorCodes.PaymentNotFoundError());
        }

        var businessName = await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == entity.TenantId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Business";

        var method = entity.Type switch
        {
            PaymentIntentType.Deposit => "Online deposit",
            PaymentIntentType.Link => "Online payment",
            PaymentIntentType.Invoice => "Online invoice",
            _ => "Online",
        };

        return Result.Success(new PaymentReceiptResponse(
            entity.Id,
            entity.BookingId,
            businessName,
            entity.Description,
            entity.Amount,
            entity.Currency,
            entity.Status.ToString(),
            entity.Status == PaymentIntentStatus.Completed ? entity.UpdatedAt : null,
            method,
            entity.ProviderReference));
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
        string? idempotencyKey,
        decimal? tipAmount,
        Guid? tipStaffMemberId,
        Guid? idempotencyCustomerId = null,
        bool useServerCallbackForCustomer = false,
        CancellationToken cancellationToken = default)
    {
        if (tipStaffMemberId is Guid tipStaffId)
        {
            var tipCheck = await ValidateTipStaffMemberAsync(tenantId, tipStaffId, cancellationToken);
            if (tipCheck.IsFailure)
            {
                return Result.Failure<PaymentIntentResponse>(tipCheck.Error);
            }
        }

        if (!useServerCallbackForCustomer)
        {
            var callbackCheck = callbackUrlValidator.ValidateOptionalCallbackUrl(callbackUrl);
            if (callbackCheck.IsFailure)
            {
                return Result.Failure<PaymentIntentResponse>(callbackCheck.Error);
            }
        }

        var normalizedIdempotencyKey = IdempotencyKeyNormalizer.Normalize(idempotencyKey);
        if (normalizedIdempotencyKey is not null)
        {
            var existingQuery = dbContext.PaymentIntents
                .AsNoTracking()
                .Where(x => x.IdempotencyKey == normalizedIdempotencyKey && x.TenantId == tenantId);

            if (idempotencyCustomerId is Guid scopedCustomerId)
            {
                existingQuery = existingQuery.Where(x =>
                    x.BookingId != null
                    && dbContext.Bookings.Any(b =>
                        b.Id == x.BookingId
                        && b.CustomerId == scopedCustomerId));
            }

            var existing = await existingQuery.FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                return Result.Success(Map(existing));
            }
        }

        var resolvedAmountResult = await ResolveAmountAsync(tenantId, bookingId, amount, currency, type, cancellationToken);
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

        var resolvedCallbackUrl = useServerCallbackForCustomer
            ? callbackUrlValidator.BuildCustomerBookingCallbackUrl(paymentIntentId)
            : string.IsNullOrWhiteSpace(callbackUrl) ? null : callbackUrl.Trim();

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
            CallbackUrl = resolvedCallbackUrl,
            IdempotencyKey = normalizedIdempotencyKey,
            TipAmount = tipAmount is > 0 ? tipAmount : null,
            TipStaffMemberId = tipStaffMemberId,
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
        if (normalizedIdempotencyKey is null)
        {
            entity.IdempotencyKey = entity.ProviderReference;
        }

        dbContext.PaymentIntents.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            entity.Id,
            entity.TenantId,
            "payment.initialized",
            "payment_intent",
            entity.Id,
            cancellationToken);

        var realtimeToken = checkoutRealtimeTokens.Issue(entity.Id, entity.ProviderReference);
        var checkoutUrl = BuildCheckoutUrlForResponse(providerResult.Value.CheckoutUrl, entity.ProviderReference, realtimeToken);
        var response = Map(entity) with
        {
            CheckoutUrl = checkoutUrl,
            CheckoutRealtimeToken = realtimeToken,
        };

        return Result.Success(response);
    }

    private async Task<Result<(decimal amount, string currency)>> ResolveAmountAsync(
        Guid tenantId,
        Guid? bookingId,
        decimal? amount,
        string currency,
        PaymentIntentType type,
        CancellationToken cancellationToken)
    {
        if (type == PaymentIntentType.Deposit && bookingId is Guid depositBookingId)
        {
            var bookingTenantMatches = await dbContext.Bookings.AsNoTracking()
                .AnyAsync(x => x.Id == depositBookingId && x.TenantId == tenantId, cancellationToken);
            if (!bookingTenantMatches)
            {
                return Result.Failure<(decimal, string)>(Error.NotFound("Booking"));
            }

            var deposit = await bookingDepositCalculator.CalculateRequiredDepositAsync(depositBookingId, cancellationToken);
            if (deposit.IsFailure)
            {
                return Result.Failure<(decimal, string)>(deposit.Error);
            }

            return Result.Success((deposit.Value!.Amount, deposit.Value.Currency));
        }

        if (amount.HasValue && amount.Value > 0)
        {
            var resolvedCurrency = string.IsNullOrWhiteSpace(currency)
                ? "NGN"
                : currency.Trim().ToUpperInvariant();
            return Result.Success((amount.Value, resolvedCurrency));
        }

        return Result.Failure<(decimal, string)>(ErrorCodes.PaymentInvalidAmountError());
    }

    private async Task<Result> ValidateTipStaffMemberAsync(
        Guid tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        var staff = await dbContext.StaffMembers.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == staffMemberId && x.TenantId == tenantId,
                cancellationToken);

        if (staff is null || !staff.IsActive)
        {
            return Result.Failure(Error.Validation("Tip staff member is not valid for this business."));
        }

        return Result.Success();
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

        await tenantRealtime.PublishPaymentCompletedAsync(
            entity.TenantId,
            entity.Id,
            entity.BookingId,
            entity.ProviderReference,
            cancellationToken);

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
        // Callers pass the payment intent id; reusing it as the audit PK made the 2nd audit write per intent
        // (initialized → confirmed) collide, so confirmation threw after the payment had already committed.
        await auditLogWriter.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                tenantId == Guid.Empty ? "system" : tenantId.ToString(),
                action,
                entityType,
                entityId?.ToString() ?? string.Empty,
                correlationContext.CorrelationId,
                DateTimeOffset.UtcNow),
            cancellationToken);
    }

    private PaymentIntentResponse Map(PaymentIntentRecord entity)
    {
        var token = checkoutRealtimeTokens.Issue(entity.Id, entity.ProviderReference);
        var stubUrl = BuildCheckoutUrl(entity);
        return new(
            entity.Id,
            entity.TenantId,
            entity.BookingId,
            entity.Type.ToString().ToLowerInvariant(),
            entity.Amount,
            entity.PlatformFeeAmount,
            entity.Currency,
            entity.Status.ToString().ToLowerInvariant(),
            BuildCheckoutUrlForResponse(stubUrl, entity.ProviderReference, token),
            entity.ProviderReference,
            entity.Description,
            entity.CreatedAt,
            token);
    }

    private string BuildCheckoutUrlForResponse(string providerOrStubUrl, string providerReference, string realtimeToken)
    {
        var checkoutUrl = string.IsNullOrWhiteSpace(providerOrStubUrl)
            ? BuildCheckoutUrlFromReference(providerReference)
            : providerOrStubUrl;

        if (ShouldEmbedRealtimeTokenInCheckoutUrl(checkoutUrl))
        {
            return AppendCheckoutRealtimeToken(checkoutUrl, realtimeToken);
        }

        return checkoutUrl;
    }

    private bool ShouldEmbedRealtimeTokenInCheckoutUrl(string checkoutUrl)
    {
        if (string.IsNullOrWhiteSpace(checkoutUrl))
        {
            return false;
        }

        if (string.Equals(paymentProvider.ProviderName, "Stub", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (checkoutUrl.StartsWith("/", StringComparison.Ordinal)
            && checkoutUrl.Contains("/checkout/stub/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var stubBase = paymentsOptions.Value.StubCheckoutBaseUrl.TrimEnd('/');
        return checkoutUrl.StartsWith(stubBase, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildCheckoutUrlFromReference(string providerReference) =>
        $"{paymentsOptions.Value.StubCheckoutBaseUrl.TrimEnd('/')}/{providerReference}";

    private static string AppendCheckoutRealtimeToken(string checkoutUrl, string token)
    {
        if (string.IsNullOrWhiteSpace(checkoutUrl))
        {
            return checkoutUrl;
        }

        var separator = checkoutUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{checkoutUrl}{separator}rt={Uri.EscapeDataString(token)}";
    }

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
