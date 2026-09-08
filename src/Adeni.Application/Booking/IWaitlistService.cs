namespace Adeni.Application.Booking;

using Adeni.Domain.Common;

public sealed record JoinWaitlistRequest(
    Guid TenantId,
    Guid ServiceOfferingId,
    DateTimeOffset? PreferredFrom,
    DateTimeOffset? PreferredTo);

public sealed record WaitlistEntryResponse(
    Guid Id,
    Guid TenantId,
    Guid ServiceOfferingId,
    DateTimeOffset? PreferredFrom,
    DateTimeOffset? PreferredTo,
    DateTimeOffset CreatedAt);

public interface IWaitlistService
{
    Task<Result<WaitlistEntryResponse>> JoinAsync(
        string customerAuth0Sub,
        JoinWaitlistRequest request,
        CancellationToken cancellationToken = default);
}
