namespace Adeni.Application.Booking;

using Adeni.Domain.Common;

public sealed record CreateQuoteRequestRequest(
    string Description,
    string? ServiceAddress,
    IReadOnlyList<string>? PhotoKeys = null);

public sealed record QuoteRequestResponse(
    Guid Id,
    Guid TenantId,
    string Description,
    string? ServiceAddress,
    IReadOnlyList<string> PhotoKeys,
    IReadOnlyList<string> PhotoUrls,
    string Status,
    decimal? QuotedAmount,
    string? QuotedCurrency,
    string? QuoteNotes,
    Guid? ServiceOfferingId,
    DateTimeOffset? ProposedStartAt,
    DateTimeOffset? ProposedEndAt,
    DateTimeOffset? ExpiresAt,
    Guid? BookingId,
    DateTimeOffset CreatedAt);

public sealed record SubmitQuoteOfferRequest(
    decimal Amount,
    string Currency,
    string? Notes,
    Guid ServiceOfferingId,
    DateTimeOffset ProposedStartAt,
    DateTimeOffset ProposedEndAt,
    DateTimeOffset? ExpiresAt = null);

public interface IQuoteRequestService
{
    Task<Result<QuoteRequestResponse>> CreateBySlugAsync(
        string customerAuth0Sub,
        string slug,
        CreateQuoteRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuoteRequestResponse>> ListForCustomerAsync(
        string customerAuth0Sub,
        CancellationToken cancellationToken = default);

    Task<Result<QuoteRequestResponse>> AcceptAsync(
        string customerAuth0Sub,
        Guid quoteRequestId,
        CancellationToken cancellationToken = default);

    Task<Result<QuoteRequestResponse>> DeclineAsync(
        string customerAuth0Sub,
        Guid quoteRequestId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuoteRequestResponse>> ListForTenantAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken = default);

    Task<Result<QuoteRequestResponse>> SubmitOfferAsync(
        Guid tenantId,
        string auth0Sub,
        Guid quoteRequestId,
        SubmitQuoteOfferRequest request,
        CancellationToken cancellationToken = default);
}
