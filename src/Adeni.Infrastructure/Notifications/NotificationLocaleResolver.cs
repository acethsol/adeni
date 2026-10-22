namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Markets;
using Adeni.Application.Notifications;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class NotificationLocaleResolver(
    AdeniDbContext dbContext,
    IMarketCatalog marketCatalog) : INotificationLocaleResolver
{
    public async Task<string> ResolveAsync(
        Guid? tenantId,
        Guid? customerId,
        string? localeHint,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(localeHint))
        {
            return NotificationTemplateText.NormalizeLocale(localeHint);
        }

        if (customerId is { } cid)
        {
            var customerLocale = await dbContext.Customers
                .AsNoTracking()
                .Where(x => x.Id == cid)
                .Select(x => x.PreferredLocale)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(customerLocale))
            {
                return NotificationTemplateText.NormalizeLocale(customerLocale);
            }
        }

        if (tenantId is { } tid)
        {
            var tenantLocale = await dbContext.TenantNotificationPreferences
                .AsNoTracking()
                .Where(x => x.TenantId == tid)
                .Select(x => x.DefaultNotificationLocale)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(tenantLocale))
            {
                return NotificationTemplateText.NormalizeLocale(tenantLocale);
            }

            var marketId = await dbContext.BusinessLocations
                .AsNoTracking()
                .Where(x => x.TenantId == tid)
                .OrderBy(x => x.CreatedAt)
                .Select(x => x.MarketId)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(marketId))
            {
                var market = marketCatalog.GetById(marketId);
                if (market?.Languages is { Count: > 0 } languages)
                {
                    return NotificationTemplateText.NormalizeLocale(languages[0]);
                }
            }
        }

        return "en";
    }
}
