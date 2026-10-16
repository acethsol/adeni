namespace Adeni.Application.Admin;

using Adeni.Domain.Common;

public sealed record AdminTransactionSearchItem(
    string Kind,
    Guid Id,
    Guid TenantId,
    string TenantName,
    string? ProviderReference,
    string? EventType,
    string? SubscriptionSku,
    long? AmountMinor,
    decimal? Amount,
    decimal? PlatformFeeAmount,
    string Currency,
    string Status,
    Guid? BookingId,
    DateTimeOffset OccurredAt);

public interface IAdminTransactionSearchService
{
    Task<Result<AdminPagedResult<AdminTransactionSearchItem>>> SearchAsync(
        string? query,
        string? kind,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
