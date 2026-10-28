namespace Adeni.Infrastructure.Finance;

using Adeni.Application.Finance;
using Adeni.Domain.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CommercePrivacyService(AdeniDbContext db) : ICommercePrivacyService
{
    public async Task StageErasureAsync(Guid customerId, CancellationToken ct)
    {
        foreach (var card in await db.Set<BusinessGiftCard>().Where(x => x.PurchaserCustomerId == customerId).ToArrayAsync(ct))
            card.PurchaserCustomerId = null;
    }
    public async Task<CommercePrivacyExport> ExportAsync(Guid customerId, CancellationToken ct) => new(await (
        from card in db.Set<BusinessGiftCard>().AsNoTracking() join tenant in db.Tenants on card.TenantId equals tenant.Id
        where card.PurchaserCustomerId == customerId
        select new GiftCardView(card.Id, tenant.Name, card.OriginalAmount, card.RemainingAmount, card.Currency, card.State.ToString(), card.CodeSuffix, card.IssuedAt)).ToArrayAsync(ct));
}
