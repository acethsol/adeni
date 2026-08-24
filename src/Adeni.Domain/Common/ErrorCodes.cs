namespace Adeni.Domain.Common;

public static class ErrorCodes
{
    public const string BookingLimitReached = "subscription.booking_limit_reached";
    public const string MultiLocationRequired = "subscription.multi_location_required";
    public const string SlotExpired = "booking.slot_expired";
    public const string SlotUnavailable = "booking.slot_unavailable";
    public const string SlotLocked = "booking.slot_locked";
    public const string AuthRequired = "auth.required";
    public const string CustomerAuthRequired = "auth.customer_required";
    public const string BusinessAccessDenied = "auth.business_access_denied";
    public const string InternalServerError = "internal.server_error";

    public static Error BookingLimitReachedError(int limit) =>
        new(
            BookingLimitReached,
            $"This business has reached its monthly booking limit ({limit}). Upgrade to Pro for unlimited bookings.",
            new Dictionary<string, object?> { ["limit"] = limit });

    public static Error MultiLocationRequiredError() =>
        new(
            MultiLocationRequired,
            "Multi-location requires the Business plan. Upgrade to add more branches.");

    public static Error SlotExpiredError() =>
        new(SlotExpired, "That time slot has passed. Please choose a new time.");

    public static Error SlotUnavailableError() =>
        new(SlotUnavailable, "That time slot is no longer available.");

    public static Error SlotLockedError() =>
        new(SlotLocked, "That time slot is being booked. Try again.");

    public static Error AuthRequiredError() =>
        new(AuthRequired, "Authentication is required.");

    public static Error CustomerAuthRequiredError() =>
        new(CustomerAuthRequired, "Customer authentication is required.");

    public static Error BusinessAccessDeniedError() =>
        new(BusinessAccessDenied, "You do not have access to this business.");

    public const string PaymentInvalidAmount = "payment.invalid_amount";
    public const string PaymentInvalidCurrency = "payment.invalid_currency";
    public const string PaymentNotFound = "payment.not_found";
    public const string PaymentProviderError = "payment.provider_error";
    public const string PaymentWebhookInvalid = "payment.webhook_invalid";
    public const string PaymentAlreadyProcessed = "payment.already_processed";
    public const string PaymentRefundNotAllowed = "payment.refund_not_allowed";
    public const string PaymentDepositNotConfigured = "payment.deposit_not_configured";

    public static Error PaymentInvalidAmountError() =>
        new(PaymentInvalidAmount, "Payment amount must be greater than zero.");

    public static Error PaymentInvalidCurrencyError() =>
        new(PaymentInvalidCurrency, "Currency must be a 3-letter ISO code.");

    public static Error PaymentNotFoundError() =>
        new(PaymentNotFound, "Payment was not found.");

    public static Error PaymentProviderFailedError() =>
        new(PaymentProviderError, "Payment could not be processed. Please try again.");

    public static Error PaymentWebhookInvalidError() =>
        new(PaymentWebhookInvalid, "Webhook signature verification failed.");

    public static Error PaymentAlreadyProcessedError() =>
        new(PaymentAlreadyProcessed, "This payment has already been processed.");

    public static Error PaymentRefundNotAllowedError() =>
        new(PaymentRefundNotAllowed, "This payment cannot be refunded.");

    public static Error PaymentDepositNotConfiguredError() =>
        new(PaymentDepositNotConfigured, "Deposits are not configured for this business.");
}
