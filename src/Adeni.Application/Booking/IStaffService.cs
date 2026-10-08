namespace Adeni.Application.Booking;

using Adeni.Domain.Common;

public sealed record StaffMemberResponse(
    Guid Id,
    string DisplayName,
    string? Title,
    string? Bio,
    bool IsActive,
    int SortOrder,
    string? AvatarImageUrl,
    IReadOnlyList<Guid> ServiceOfferingIds);

public sealed record CreateStaffMemberRequest(
    string DisplayName,
    string? Title = null,
    string? Bio = null,
    int SortOrder = 0,
    IReadOnlyList<Guid>? ServiceOfferingIds = null);

public sealed record UpdateStaffMemberRequest(
    string DisplayName,
    string? Title,
    string? Bio,
    int SortOrder,
    bool IsActive);

public interface IStaffService
{
    Task<IReadOnlyList<StaffMemberResponse>> ListForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<StaffMemberResponse>>> ListPublicBySlugAsync(
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
}
