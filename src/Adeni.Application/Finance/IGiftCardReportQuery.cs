namespace Adeni.Application.Finance;
using Adeni.Domain.Common;
public sealed record GiftCardReportRow(Guid CardId, string Currency, decimal IssuedValue, decimal OutstandingValue,
    decimal RedeemedValue, string State, DateTimeOffset? IssuedAt);
public interface IGiftCardReportQuery
{
    Task<Result<IReadOnlyList<GiftCardReportRow>>> GetAsync(Guid tenant, string actor, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
