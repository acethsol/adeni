namespace Adeni.Api.Errors;

using Adeni.Api.Middleware;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Maps domain <see cref="Error"/> values to RFC 7807 JSON bodies returned by the API.
/// </summary>
internal static class ApiErrorResponseMapper
{
    public static ObjectResult ToActionResult(Error error, HttpContext httpContext)
    {
        var statusCode = ResolveStatusCode(error.Code);
        var problem = ToProblemDetails(error, statusCode, httpContext);
        return new ObjectResult(problem) { StatusCode = statusCode };
    }

    public static ProblemDetails ToProblemDetails(
        Error error,
        int statusCode,
        HttpContext? httpContext = null)
    {
        var correlationId = ResolveCorrelationId(httpContext);
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = error.Message,
            Status = statusCode,
            Detail = error.Message,
        };

        problem.Extensions["code"] = error.Code;

        if (error.Params is { Count: > 0 })
        {
            problem.Extensions["params"] = error.Params;
        }

        if (correlationId is not null)
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        return problem;
    }

    public static int ResolveStatusCode(string code) =>
        code switch
        {
            ErrorCodes.CommerceValidation => StatusCodes.Status400BadRequest,
            ErrorCodes.CommerceConflict => StatusCodes.Status409Conflict,
            ErrorCodes.CommerceSettlementRequired => StatusCodes.Status403Forbidden,
            ErrorCodes.CommerceCredentialInvalid => StatusCodes.Status404NotFound,
            "intake.conflict" or "intake.submission_closed" => StatusCodes.Status409Conflict,
            "intake.validation" => StatusCodes.Status400BadRequest,
            "validation" => StatusCodes.Status400BadRequest,
            "not_found" => StatusCodes.Status404NotFound,
            "forbidden" => StatusCodes.Status403Forbidden,
            "conflict" => StatusCodes.Status409Conflict,
            ErrorCodes.InternalServerError => StatusCodes.Status500InternalServerError,
            _ when code.StartsWith("validation.", StringComparison.Ordinal) =>
                StatusCodes.Status400BadRequest,
            _ when code.StartsWith("not_found.", StringComparison.Ordinal) =>
                StatusCodes.Status404NotFound,
            _ when code.StartsWith("conflict.", StringComparison.Ordinal) =>
                StatusCodes.Status409Conflict,
            _ when code.StartsWith("subscription.", StringComparison.Ordinal) =>
                StatusCodes.Status403Forbidden,
            _ when code.StartsWith("auth.", StringComparison.Ordinal) =>
                StatusCodes.Status403Forbidden,
            _ when code.StartsWith("permission.", StringComparison.Ordinal) =>
                StatusCodes.Status403Forbidden,
            _ when code.StartsWith("forbidden.", StringComparison.Ordinal) =>
                StatusCodes.Status403Forbidden,
            _ when code.StartsWith("entitlement.", StringComparison.Ordinal) =>
                StatusCodes.Status403Forbidden,
            _ when code.StartsWith("entitlements.", StringComparison.Ordinal) =>
                StatusCodes.Status403Forbidden,
            _ when code.StartsWith("internal.", StringComparison.Ordinal) =>
                StatusCodes.Status500InternalServerError,
            _ when code.StartsWith("booking.", StringComparison.Ordinal) => ResolveBookingStatusCode(code),
            _ when code.StartsWith("staff.", StringComparison.Ordinal) => ResolveStaffStatusCode(code),
            _ when code.StartsWith("payment.", StringComparison.Ordinal) => ResolvePaymentStatusCode(code),
            _ when code.StartsWith("messaging.", StringComparison.Ordinal) => ResolveMessagingStatusCode(code),
            _ when code.StartsWith("retention.", StringComparison.Ordinal) => ResolveRetentionStatusCode(code),
            _ when code.StartsWith("attendance.", StringComparison.Ordinal) => ResolveAttendanceStatusCode(code),
            _ => StatusCodes.Status404NotFound,
        };

    private static int ResolveAttendanceStatusCode(string code) =>
        code switch
        {
            ErrorCodes.AttendanceInvalidTransition => StatusCodes.Status409Conflict,
            ErrorCodes.AttendanceStaffNotLinked => StatusCodes.Status403Forbidden,
            ErrorCodes.AttendanceLocationNotConfigured => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };

    private static int ResolveStaffStatusCode(string code) =>
        code switch
        {
            ErrorCodes.StaffLeaveNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.StaffLeaveOverlap => StatusCodes.Status409Conflict,
            ErrorCodes.StaffHoursInvalid => StatusCodes.Status400BadRequest,
            ErrorCodes.StaffInviteInvalid => StatusCodes.Status400BadRequest,
            ErrorCodes.StaffInviteExpired => StatusCodes.Status410Gone,
            ErrorCodes.StaffInviteEmailMismatch => StatusCodes.Status403Forbidden,
            ErrorCodes.StaffLastOwner => StatusCodes.Status409Conflict,
            ErrorCodes.StaffLocationVerificationFailed => StatusCodes.Status403Forbidden,
            ErrorCodes.StaffPresenceRequired => StatusCodes.Status403Forbidden,
            ErrorCodes.StaffPresenceExpired => StatusCodes.Status403Forbidden,
            ErrorCodes.StaffPresenceLocationMismatch => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };

    private static int ResolvePaymentStatusCode(string code) =>
        code switch
        {
            ErrorCodes.PaymentNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.PaymentAlreadyProcessed => StatusCodes.Status409Conflict,
            ErrorCodes.PaymentRefundNotAllowed => StatusCodes.Status409Conflict,
            ErrorCodes.PaymentWebhookInvalid => StatusCodes.Status400BadRequest,
            ErrorCodes.PaymentProviderError => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status400BadRequest,
        };

    private static int ResolveBookingStatusCode(string code) =>
        code switch
        {
            ErrorCodes.SlotExpired => StatusCodes.Status400BadRequest,
            ErrorCodes.SlotUnavailable or ErrorCodes.SlotLocked or ErrorCodes.StaffUnavailable
                or ErrorCodes.CapacityFull =>
                StatusCodes.Status409Conflict,
            ErrorCodes.StaffNotEligible or ErrorCodes.CartEmpty or ErrorCodes.AddonRequiresParent
                or ErrorCodes.GuestLimit =>
                StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };

    private static int ResolveRetentionStatusCode(string code) =>
        code switch
        {
            RetentionErrorCodes.PackageNotFound or RetentionErrorCodes.BenefitNotFound
                or RetentionErrorCodes.MembershipPlanNotFound or RetentionErrorCodes.MembershipNotFound =>
                StatusCodes.Status404NotFound,
            RetentionErrorCodes.OwnerRequired => StatusCodes.Status403Forbidden,
            RetentionErrorCodes.OnlinePurchaseUnavailable or RetentionErrorCodes.OnlineSubscriptionUnavailable =>
                StatusCodes.Status503ServiceUnavailable,
            RetentionErrorCodes.BenefitInsufficient or RetentionErrorCodes.BenefitConflict
                or RetentionErrorCodes.IdempotencyConflict or RetentionErrorCodes.PackageInactive
                or RetentionErrorCodes.PackageNotActive or RetentionErrorCodes.PackageExpired
                or RetentionErrorCodes.ScopeLocationDenied or RetentionErrorCodes.SellLocationDenied
                or RetentionErrorCodes.MembershipPlanInactive or RetentionErrorCodes.MembershipAlreadyActive
                or RetentionErrorCodes.MembershipInvalidState or RetentionErrorCodes.MembershipFreezeNotAllowed
                or RetentionErrorCodes.MembershipRenewalNotDue or RetentionErrorCodes.MembershipNotRedeemable
                or RetentionErrorCodes.LoyaltyProgramNotConfigured or RetentionErrorCodes.LoyaltyInsufficientPoints
                or RetentionErrorCodes.LoyaltyConflict or RetentionErrorCodes.LoyaltyRedeemInvalid
                or RetentionErrorCodes.LoyaltyProgramDisabled =>
                StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

    private static int ResolveMessagingStatusCode(string code) =>
        code switch
        {
            ErrorCodes.ConversationNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.ConversationClosed => StatusCodes.Status409Conflict,
            ErrorCodes.MessageBodyInvalid => StatusCodes.Status400BadRequest,
            ErrorCodes.MessageAttachmentInvalid => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };

    private static string? ResolveCorrelationId(HttpContext? httpContext)
    {
        if (httpContext?.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value) == true
            && value is string correlationId
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        return null;
    }
}
