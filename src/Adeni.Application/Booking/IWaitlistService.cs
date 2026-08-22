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

public sealed record CreateQuoteRequestRequest(
    string Description,
    string? ServiceAddress);

public sealed record QuoteRequestResponse(
    Guid Id,
    Guid TenantId,
    string Description,
    string? ServiceAddress,
    DateTimeOffset CreatedAt);

public interface IWaitlistService
{
    Task<Result<WaitlistEntryResponse>> JoinAsync(
        string customerAuth0Sub,
        JoinWaitlistRequest request,
        CancellationToken cancellationToken = default);
}

public interface IQuoteRequestService
{
    Task<Result<QuoteRequestResponse>> CreateBySlugAsync(
        string customerAuth0Sub,
        string slug,
        CreateQuoteRequestRequest request,
        CancellationToken cancellationToken = default);
}
