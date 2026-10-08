namespace Adeni.Application.Booking;

using Adeni.Domain.Common;

public sealed record ServiceMenuGroupResponse(Guid Id, string Name, int SortOrder);

public sealed record CreateServiceMenuGroupRequest(string Name, int SortOrder = 0);

public sealed record UpdateServiceMenuGroupRequest(string Name, int SortOrder);

public interface IServiceMenuGroupService
{
    Task<IReadOnlyList<ServiceMenuGroupResponse>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result<ServiceMenuGroupResponse>> CreateAsync(
        Guid tenantId,
        CreateServiceMenuGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ServiceMenuGroupResponse>> UpdateAsync(
        Guid tenantId,
        Guid groupId,
        UpdateServiceMenuGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        Guid tenantId,
        Guid groupId,
        CancellationToken cancellationToken = default);
}
