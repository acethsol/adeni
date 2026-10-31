namespace Adeni.Domain.Common;

public static class ErrorCodes
{
    public const string CalendarUnavailable = "calendar.unavailable";
    public const string CalendarAuthorizationInvalid = "calendar.authorization_invalid";
    public const string CommerceValidation = "commerce.validation";
    public const string CommerceConflict = "commerce.conflict";
    public const string CommerceSettlementRequired = "commerce.settlement_required";
    public const string CommerceCredentialInvalid = "commerce.credential_invalid";

    public const string BookingLimitReached = "subscription.booking_limit_reached";
    public const string MultiLocationRequired = "subscription.multi_location_required";
    public const string SlotExpired = "booking.slot_expired";
    public const string SlotUnavailable = "booking.slot_unavailable";
    public const string SlotLocked = "booking.slot_locked";
    public const string BookingClosed = "booking.closed";
    public const string StaffUnavailable = "booking.staff_unavailable";
    public const string StaffNotEligible = "booking.staff_not_eligible";
    public const string StaffHoursInvalid = "staff.hours_invalid";
    public const string StaffLeaveOverlap = "staff.leave_overlap";
    public const string StaffLeaveNotFound = "staff.leave_not_found";
    public const string AttendanceInvalidTransition = "attendance.invalid_transition";
    public const string AttendanceStaffNotLinked = "attendance.staff_not_linked";
    public const string AttendanceNotEntitled = "entitlements.attendance_required";
    public const string StaffLocationVerificationFailed = "staff.location_verification_failed";
    public const string AttendanceLocationNotConfigured = "attendance.location_not_configured";
    public const string StaffPresenceRequired = "staff.presence_required";
    public const string StaffPresenceExpired = "staff.presence_expired";
    public const string StaffPresenceLocationMismatch = "staff.presence_location_mismatch";
    public const string CartEmpty = "booking.cart_empty";
    public const string AddonRequiresParent = "booking.addon_requires_parent";
    public const string GuestLimit = "booking.guest_limit";
    public const string CapacityFull = "booking.capacity_full";
    public const string QueueClosed = "queue.closed";
    public const string QueueFull = "queue.full";
    public const string QueueNoCapacity = "queue.no_capacity";
    public const string AuthRequired = "auth.required";

    public const int MaxBookingGuests = 6;
    public const int MaxBookingLines = 10;
    public const string CustomerAuthRequired = "auth.customer_required";
    public const string BusinessAccessDenied = "auth.business_access_denied";
    public const string PermissionDenied = "permission.denied";
    public const string InternalServerError = "internal.server_error";

    public static Error BookingLimitReachedError(int limit) =>
        new(
            BookingLimitReached,
            $"This business has reached its monthly booking limit ({limit}). Upgrade to Pro for unlimited bookings.",
            new Dictionary<string, object?> { ["limit"] = limit });

    public static Error MultiLocationRequiredError() =>
        new(
            MultiLocationRequired,
            "Your plan does not include multiple locations.");

    public const string AnalyticsNotEntitled = "entitlements.analytics_required";
    public const string CustomerSegmentationNotEntitled = "entitlements.customer_segmentation";
    public const string CustomerHistoryLimited = "entitlements.customer_history";

    public static Error AnalyticsNotEntitledError() =>
        new(AnalyticsNotEntitled, "Advanced analytics is not included on your current plan.");

    public const string ExportsNotEntitled = "entitlements.exports";
    public const string ExcelExportNotEntitled = "entitlements.excel_export";
    public const string MultiLocationReportingNotEntitled = "entitlements.multi_location_reporting";
    public const string ReportNotFound = "reporting.report_not_found";
    public const string ReportFormatNotSupported = "reporting.format_not_supported";

    public static Error ExportsNotEntitledError() =>
        new(ExportsNotEntitled, "Exports are not included on your current plan.");

    public static Error ExcelExportNotEntitledError() =>
        new(ExcelExportNotEntitled, "Excel export is not included on your current plan.");

    public static Error MultiLocationReportingNotEntitledError() =>
        new(
            MultiLocationReportingNotEntitled,
            "Cross-location reporting is not included on your current plan.");

    public static Error ReportNotFoundError(string reportKey) =>
        new(ReportNotFound, $"Report \"{reportKey}\" is not available.", new Dictionary<string, object?> { ["reportKey"] = reportKey });

    public static Error ReportFormatNotSupportedError(string reportKey, string format) =>
        new(
            ReportFormatNotSupported,
            $"Format \"{format}\" is not supported for report \"{reportKey}\".",
            new Dictionary<string, object?> { ["reportKey"] = reportKey, ["format"] = format });

    public static Error CustomerSegmentationNotEntitledError() =>
        new(CustomerSegmentationNotEntitled, "Customer segmentation is not included on your current plan.");

    public static Error CustomerHistoryLimitedError() =>
        new(CustomerHistoryLimited, "Full customer history is not included on your current plan.");

    public const string LocationLimitReached = "entitlements.location_limit";
    public const string PractitionerLimitReached = "entitlements.practitioner_limit";
    public const string WaitlistNotEntitled = "entitlements.waitlist";
    public const string SmsNotEntitled = "entitlements.sms";

    public static Error LocationLimitReachedError(int maxLocations) =>
        new(
            LocationLimitReached,
            $"Your plan allows up to {maxLocations} active location(s).",
            new Dictionary<string, object?> { ["maxLocations"] = maxLocations });

    public static Error PractitionerLimitReachedError(int maxPractitioners) =>
        new(
            PractitionerLimitReached,
            $"Your plan allows up to {maxPractitioners} active team members.",
            new Dictionary<string, object?> { ["maxPractitioners"] = maxPractitioners });

    public static Error WaitlistNotEntitledError() =>
        new(WaitlistNotEntitled, "Waitlist is not included on your current plan.");

    public static Error SmsNotEntitledError() =>
        new(SmsNotEntitled, "SMS notifications are not included on your current plan.");

    public static Error SlotExpiredError() =>
        new(SlotExpired, "That time slot has passed. Please choose a new time.");

    public static Error SlotUnavailableError() =>
        new(SlotUnavailable, "That time slot is no longer available.");

    public static Error SlotLockedError() =>
        new(SlotLocked, "That time slot is being booked. Try again.");

    public static Error BookingClosedError() =>
        new(BookingClosed, "This business is not accepting online bookings right now.");

    public static Error StaffUnavailableError() =>
        new(StaffUnavailable, "That team member is not available for the selected time.");

    public static Error StaffNotEligibleError() =>
        new(StaffNotEligible, "That team member does not offer this service.");

    public static Error StaffHoursInvalidError(string message = "Staff hours are invalid.") =>
        new(StaffHoursInvalid, message);

    public static Error StaffLeaveOverlapError() =>
        new(StaffLeaveOverlap, "That leave range overlaps an existing leave entry.");

    public static Error StaffLeaveNotFoundError() =>
        new(StaffLeaveNotFound, "Leave entry was not found.");

    public static Error AttendanceInvalidTransitionError() =>
        new(AttendanceInvalidTransition, "That attendance action is not allowed in the current state.");

    public static Error AttendanceStaffNotLinkedError() =>
        new(AttendanceStaffNotLinked, "Link your portal login to a staff profile to use attendance.");

    public static Error AttendanceNotEntitledError() =>
        new(AttendanceNotEntitled, "Staff attendance is not included on your plan.");

    public static Error StaffLocationVerificationFailedError(int? distanceMeters = null, int? radiusMeters = null) =>
        new(
            StaffLocationVerificationFailed,
            "Location verification failed. You must be at the business location to clock in.",
            distanceMeters is null && radiusMeters is null
                ? null
                : new Dictionary<string, object?>
                {
                    ["distanceMeters"] = distanceMeters,
                    ["radiusMeters"] = radiusMeters,
                });

    public static Error AttendanceLocationNotConfiguredError() =>
        new(
            AttendanceLocationNotConfigured,
            "This location does not have map coordinates configured for attendance geofence.");

    public static Error StaffPresenceRequiredError() =>
        new(StaffPresenceRequired, "On-premises verification is required. Verify your location at the branch.");

    public static Error StaffPresenceExpiredError() =>
        new(StaffPresenceExpired, "Your on-premises verification expired. Verify again at the branch.");

    public static Error StaffPresenceLocationMismatchError() =>
        new(
            StaffPresenceLocationMismatch,
            "Your verification is for a different branch. Verify at this location.");

    public static Error CartEmptyError() =>
        new(CartEmpty, "Add at least one service to book.");

    public static Error AddonRequiresParentError() =>
        new(AddonRequiresParent, "Add-ons need a main service in your booking.");

    public static Error GuestLimitError(int max = MaxBookingGuests) =>
        new(
            GuestLimit,
            $"Party size must be between 1 and {max}.",
            new Dictionary<string, object?> { ["max"] = max });

    public static Error CapacityFullError() =>
        new(CapacityFull, "This session is full for the selected party size.");

    public static Error QueueClosedError() =>
        new(QueueClosed, "Walk-ins are not accepting new guests right now.");

    public static Error QueueFullError() =>
        new(QueueFull, "The waiting list is full. Try again later.");

    public static Error QueueNoCapacityError() =>
        new(
            QueueNoCapacity,
            "Walk-ins would conflict with upcoming appointments. Try again later or book an appointment.");

    public static Error AuthRequiredError() =>
        new(AuthRequired, "Authentication is required.");

    public static Error CustomerAuthRequiredError() =>
        new(CustomerAuthRequired, "Customer authentication is required.");

    public const string CustomerEmailNotVerified = "customer.email_not_verified";
    public const string CustomerPhoneNotVerified = "customer.phone_not_verified";
    public const string CustomerVerificationRequired = "customer.verification_required";
    public const string QueuePhoneNotVerified = "queue.phone_not_verified";

    public static Error CustomerEmailNotVerifiedError() =>
        new(CustomerEmailNotVerified, "Verify your email before continuing.");

    public static Error CustomerPhoneNotVerifiedError() =>
        new(CustomerPhoneNotVerified, "Verify your phone number before continuing.");

    public static Error CustomerVerificationRequiredError() =>
        new(CustomerVerificationRequired, "Account verification is required for this action.");

    public static Error QueuePhoneNotVerifiedError() =>
        new(QueuePhoneNotVerified, "Phone verification is required to join the queue.");

    public static Error BusinessAccessDeniedError() =>
        new(BusinessAccessDenied, "You do not have access to this business.");

    public static Error PermissionDeniedError() =>
        new(PermissionDenied, "You do not have permission to do that.");

    public const string StaffInviteInvalid = "staff.invite_invalid";
    public const string StaffInviteExpired = "staff.invite_expired";
    public const string StaffInviteEmailMismatch = "staff.invite_email_mismatch";
    public const string StaffLastOwner = "staff.last_owner";

    public static Error StaffInviteInvalidError() =>
        new(StaffInviteInvalid, "This invite link is invalid or no longer available.");

    public static Error StaffInviteExpiredError() =>
        new(StaffInviteExpired, "This invite has expired. Ask the business owner to resend it.");

    public static Error StaffInviteEmailMismatchError() =>
        new(StaffInviteEmailMismatch, "Sign in with the email address this invite was sent to.");

    public static Error StaffLastOwnerError() =>
        new(StaffLastOwner, "Cannot remove or demote the last owner of this business.");

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

    public const string MessagingNotEntitled = "subscription.messaging_required";
    public const string ConversationNotFound = "messaging.conversation_not_found";
    public const string ConversationClosed = "messaging.conversation_closed";
    public const string MessageBodyInvalid = "messaging.message_body_invalid";
    public const string MessageAttachmentInvalid = "messaging.message_attachment_invalid";

    public static Error MessagingNotEntitledError() =>
        new(
            MessagingNotEntitled,
            "In-app messaging is not included on your current plan.");

    public static Error ConversationNotFoundError() =>
        new(ConversationNotFound, "Conversation was not found.");

    public static Error ConversationClosedError() =>
        new(ConversationClosed, "This conversation is closed.");

    public static Error MessageBodyInvalidError() =>
        new(MessageBodyInvalid, "Message must be between 1 and 4000 characters, or include an attachment.");

    public static Error MessageAttachmentInvalidError(string detail) =>
        new(MessageAttachmentInvalid, detail);

    public const string PublicPageInvalidTemplate = "tenancy.public_page.invalid_template";
    public const string PublicPageInvalidAccent = "tenancy.public_page.invalid_accent";
    public const string PublicPageSectionsRequired = "tenancy.public_page.sections_required";

    public static Error PublicPageInvalidTemplateError() =>
        new(PublicPageInvalidTemplate, "Choose a valid public page template.");

    public static Error PublicPageInvalidAccentError() =>
        new(PublicPageInvalidAccent, "Accent color must be a hex value like #0F766E.");

    public static Error PublicPageSectionsRequiredError() =>
        new(
            PublicPageSectionsRequired,
            "Keep at least one of About, Services, or Visit visible.");

    public const string ListSortInvalid = "list.sort_invalid";

    public static Error ListSortInvalidError(string? sortBy) =>
        new(
            ListSortInvalid,
            string.IsNullOrWhiteSpace(sortBy)
                ? "Sort direction must be asc or desc."
                : $"Sort field \"{sortBy}\" is not allowed for this list.");
}
