namespace Adeni.Infrastructure.Finance;
using Adeni.Application.Finance;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Domain.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
public sealed class CustomerGiftCardQuery(AdeniDbContext db, IFinanceCommerceAccessQuery access) : ICustomerGiftCardQuery
{
    public async Task<Result<IReadOnlyList<GiftCardView>>> GetPurchasedAsync(CustomerProfileScope scope, string actor, CancellationToken ct)
    {
        if(!await access.CanReadAsync(scope.TenantId,actor,ct))return Result.Failure<IReadOnlyList<GiftCardView>>(Error.Forbidden("Finance access is required."));
        if(scope.CustomerId is null || scope.LocationId is not null)return Result.Success<IReadOnlyList<GiftCardView>>([]);
        return Result.Success<IReadOnlyList<GiftCardView>>(await (from card in db.Set<BusinessGiftCard>().AsNoTracking()
            join tenant in db.Tenants on card.TenantId equals tenant.Id
            where card.TenantId==scope.TenantId && card.PurchaserCustomerId==scope.CustomerId && card.IssuedAt!=null
            orderby card.IssuedAt descending,card.Id
            select new GiftCardView(card.Id,tenant.Name,card.OriginalAmount,card.RemainingAmount,card.Currency,card.State.ToString(),card.CodeSuffix,card.IssuedAt,
                db.BusinessLocations.Where(l=>l.TenantId==card.TenantId&&l.IsActive).OrderByDescending(l=>l.IsPrimary).Select(l=>l.Slug).FirstOrDefault())).Take(25).ToArrayAsync(ct));
    }
}
