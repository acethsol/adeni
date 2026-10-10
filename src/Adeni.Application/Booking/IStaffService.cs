namespace Adeni.Application.Booking;

using Adeni.Domain.Common;

public sealed record StaffMemberResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string DisplayName,
    string RoleKey,
    string? Title,
    string? Bio,
    bool IsActive,
    int SortOrder,
    string? AvatarImageUrl,
    IReadOnlyList<Guid> ServiceOfferingIds,
    string PortalAccessStatus = "none",
    Guid? PortalInviteId = null,
    Guid? BusinessUserId = null,
    string? PermissionRole = null);

/// <summary>Public discovery shape — no first/last name.</summary>
public sealed record PublicStaffMemberResponse(
    Guid Id,
    string DisplayName,
    string RoleKey,
    string? Title,
    string? Bio,
    int SortOrder,
    string? AvatarImageUrl,
    IReadOnlyList<Guid> ServiceOfferingIds);

public sealed record CreateStaffMemberRequest(
    string FirstName,
    string LastName,
    string? DisplayName = null,
    string? RoleKey = null,
    string? Title = null,
    string? Bio = null,
    int SortOrder = 0,
    IReadOnlyList<Guid>? ServiceOfferingIds = null);

public sealed record UpdateStaffMemberRequest(
    string FirstName,
    string LastName,
    string DisplayName,
    string RoleKey,
    string? Title,
    string? Bio,
    int SortOrder,
    bool IsActive);

public sealed record StaffLeaveResponse(
    Guid Id,
    Guid StaffMemberId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? Reason,
    DateTimeOffset CreatedAt,
    IReadOnlyList<Guid> ConflictingBookingIds);

public sealed record CreateStaffLeaveRequest(
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? Reason = null);

public sealed record StaffCalendarBookingItem(
    Guid Id,
    string ServiceName,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    int Status,
    string? CustomerNotes);

public sealed record StaffCalendarLeaveItem(
    Guid Id,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? Reason);

public sealed record StaffCalendarResponse(
    Guid StaffMemberId,
    string DisplayName,
    IReadOnlyList<WeeklyAvailabilityRule> Hours,
    bool InheritsBusinessHours,
    IReadOnlyList<StaffCalendarBookingItem> Bookings,
    IReadOnlyList<StaffCalendarLeaveItem> Leave);

public interface IStaffService
{
    Task<IReadOnlyList<StaffMemberResponse>> ListForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PublicStaffMemberResponse>>> ListPublicBySlugAsync(
        string slug,
        Guid? serviceOfferingId = null,
        CancellationToken cancellationToken = default);

    Task<Result<StaffMemberResponse>> CreateAsync(
        Guid tenantId,
        CreateStaffMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<StaffMemberResponse>> UpdateAsync(
        Guid tenantId,
        Guid staffMemberId,
        UpdateStaffMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeactivateAsync(
        Guid tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken = default);

    Task<Result<StaffMemberResponse>> ReplaceServicesAsync(
        Guid tenantId,
        Guid staffMemberId,
        IReadOnlyList<Guid> serviceOfferingIds,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WeeklyAvailabilityRule>>> GetHoursAsync(
        Guid tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WeeklyAvailabilityRule>>> ReplaceHoursAsync(
        Guid tenantId,
        Guid staffMemberId,
        IReadOnlyList<WeeklyAvailabilityRule> rules,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<StaffLeaveResponse>>> ListLeaveAsync(
        Guid tenantId,
        Guid staffMemberId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    Task<Result<StaffLeaveResponse>> CreateLeaveAsync(
        Guid tenantId,
        Guid staffMemberId,
        CreateStaffLeaveRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteLeaveAsync(
        Guid tenantId,
        Guid staffMemberId,
        Guid leaveId,
        CancellationToken cancellationToken = default);

    Task<Result<StaffCalendarResponse>> GetCalendarAsync(
        Guid tenantId,
        Guid staffMemberId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
