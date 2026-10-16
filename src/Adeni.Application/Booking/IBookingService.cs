namespace Adeni.Application.Booking;

using Adeni.Application.Common;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;

public sealed record BookingLineResponse(
    Guid ServiceOfferingId,
    string ServiceName,
    decimal PriceAmount,
    string Currency,
    int DurationMinutes,
    int SortOrder,
    bool IsAddOn);

public sealed record BookingGuestResponse(string? DisplayName, int SortOrder);

public sealed record BookingResponse(
    Guid Id,
    Guid TenantId,
    Guid ServiceOfferingId,
    string ServiceName,
    Guid CustomerId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    BookingStatus Status,
    string? CustomerNotes,
    DateTimeOffset CreatedAt,
    Guid? StaffMemberId = null,
    string? StaffDisplayName = null,
    int GuestCount = 1,
    IReadOnlyList<BookingLineResponse>? Lines = null,
    IReadOnlyList<BookingGuestResponse>? Guests = null,
    decimal? TotalPriceAmount = null,
    string? Currency = null,
    string? SettlementSource = null,
    DateTimeOffset? PaidAt = null,
    string? ExternalPaymentReference = null,
    decimal? RemainingBalanceAmount = null,
    string? CustomerName = null,
    string? CustomerPhoneMasked = null);

public sealed record MarkBookingPaidRequest(
    string? ExternalReference = null,
    string? Note = null);

public sealed record CollectBalanceResponse(
    Guid BookingId,
    Guid PaymentIntentId,
    decimal Amount,
    string Currency,
    string CheckoutUrl,
    string ProviderReference,
    BookingResponse Booking);

public sealed record CustomerBookingResponse(
    Guid Id,
    Guid TenantId,
    string BusinessName,
    string BusinessSlug,
    Guid ServiceOfferingId,
    string ServiceName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    BookingStatus Status,
    string? CustomerNotes,
    DateTimeOffset CreatedAt,
    bool CanReview = false,
    bool HasReview = false,
    byte? ReviewRating = null,
    Guid? StaffMemberId = null,
    string? StaffDisplayName = null,
    int GuestCount = 1,
    IReadOnlyList<BookingLineResponse>? Lines = null,
    IReadOnlyList<BookingGuestResponse>? Guests = null,
    decimal? TotalPriceAmount = null,
    string? Currency = null);

public sealed record CreateBookingLineRequest(
    Guid ServiceOfferingId,
    Guid? StaffMemberId = null);

public sealed record CreateBookingGuestRequest(string? DisplayName = null);

public sealed record CreateBookingRequest(
    Guid TenantId,
    Guid ServiceOfferingId,
    DateTimeOffset StartAt,
    string? CustomerNotes = null,
    Guid? StaffMemberId = null,
    IReadOnlyList<CreateBookingLineRequest>? Lines = null,
    int GuestCount = 1,
    IReadOnlyList<CreateBookingGuestRequest>? Guests = null);

public interface IBookingService
{
    Task<Result<BookingResponse>> CreateAsync(
        string customerAuth0Sub,
        CreateBookingRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<BookingResponse>> ListForTenantAsync(
        Guid tenantId,
        string tab = TenantBookingTabs.Pending,
        int page = 1,
        int pageSize = Paging.DefaultPageSize,
        Guid? staffMemberId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerBookingResponse>> ListForCustomerAsync(
        string customerAuth0Sub,
        CancellationToken cancellationToken = default);

    Task<Result<BookingResponse>> AcceptAsync(
        Guid tenantId,
        Guid bookingId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default);

    Task<Result<BookingResponse>> RejectAsync(
        Guid tenantId,
        Guid bookingId,
        string? reason,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default);

    Task<Result<BookingResponse>> CheckInAsync(
        Guid tenantId,
        Guid bookingId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default);

    Task<Result<BookingResponse>> CompleteAsync(
        Guid tenantId,
        Guid bookingId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default);

    Task<Result<BookingResponse>> MarkPaidAsync(
        Guid tenantId,
        Guid bookingId,
        MarkBookingPaidRequest request,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default);

    Task<Result<CollectBalanceResponse>> CollectBalanceAsync(
        Guid tenantId,
        Guid bookingId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerBookingResponse>> CancelAsync(
        string customerAuth0Sub,
        Guid bookingId,
        CancellationToken cancellationToken = default);
}
