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
}
