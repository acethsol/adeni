namespace Adeni.Application.Admin;

using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;

public sealed record PendingBusinessResponse(
    Guid Id,
    string Name,
    TenantStatus Status,
    DateTimeOffset CreatedAt);

public interface IAdminBusinessService
{
    Task<IReadOnlyList<PendingBusinessResponse>> GetPendingVerificationsAsync(
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> ApproveAsync(Guid tenantId, string adminId, CancellationToken cancellationToken = default);

    Task<Result<Unit>> RejectAsync(
        Guid tenantId,
        string adminId,
        string reason,
        CancellationToken cancellationToken = default);
}

public readonly record struct Unit
{
    public static Unit Value => default;
}
