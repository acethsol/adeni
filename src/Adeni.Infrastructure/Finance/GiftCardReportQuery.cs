namespace Adeni.Infrastructure.Finance;
using Adeni.Application.Finance;
using Adeni.Domain.Common;
using Adeni.Domain.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
public sealed class GiftCardReportQuery(AdeniDbContext db, IFinanceCommerceAccessQuery access) : IGiftCardReportQuery
{
    public async Task<Result<IReadOnlyList<GiftCardReportRow>>> GetAsync(Guid tenant, string actor, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        if(!await access.CanReadAsync(tenant,actor,ct))return Result.Failure<IReadOnlyList<GiftCardReportRow>>(Error.Forbidden("Finance access is required."));
        if(to < from || (to-from).TotalDays>366)return Result.Failure<IReadOnlyList<GiftCardReportRow>>(CommerceErrors.Invalid);
        var cards=await db.Set<BusinessGiftCard>().AsNoTracking().Where(x=>x.TenantId==tenant && x.IssuedAt>=from && x.IssuedAt<=to)
            .OrderBy(x=>x.IssuedAt).ThenBy(x=>x.Id).Take(10001).ToArrayAsync(ct);
        if(cards.Length>10000)return Result.Failure<IReadOnlyList<GiftCardReportRow>>(Error.Validation("Narrow the report date range."));
        var ids=cards.Select(x=>x.Id).ToArray();
        var redeemed=await db.Set<GiftCardTransaction>().AsNoTracking().Where(x=>x.TenantId==tenant && ids.Contains(x.GiftCardId) && (x.Kind==GiftTransactionKind.Redeem || x.Kind==GiftTransactionKind.RedemptionReversal))
            .GroupBy(x=>x.GiftCardId).Select(x=>new {Id=x.Key,Value=-x.Sum(t=>t.Delta)}).ToDictionaryAsync(x=>x.Id,x=>x.Value,ct);
        return Result.Success<IReadOnlyList<GiftCardReportRow>>(cards.Select(c=>new GiftCardReportRow(c.Id,c.Currency,c.OriginalAmount,c.RemainingAmount,redeemed.GetValueOrDefault(c.Id),c.State.ToString(),c.IssuedAt)).ToArray());
    }
}
