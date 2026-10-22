namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Markets;
using Adeni.Application.Notifications;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class NotificationSenderConfigResolver(
    AdeniDbContext dbContext,
    IMarketCatalog marketCatalog,
    IOptions<NotificationsOptions> options) : INotificationSenderConfigResolver
{
    public async Task<ResolvedSenderConfig> ResolveForTenantAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        var country = config.DefaultCountryCode.Trim().ToUpperInvariant();

        if (tenantId is { } tid)
        {
            var marketId = await dbContext.BusinessLocations
                .AsNoTracking()
                .Where(x => x.TenantId == tid && x.IsPrimary && x.IsActive)
                .Select(x => x.MarketId)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(marketId))
            {
                var market = marketCatalog.GetById(marketId);
                if (market is not null && !string.IsNullOrWhiteSpace(market.CountryCode))
                {
                    country = market.CountryCode.Trim().ToUpperInvariant();
                }
            }
        }

        var marketRow = config.Senders.FirstOrDefault(s =>
            string.Equals(s.CountryCode.Trim(), country, StringComparison.OrdinalIgnoreCase));

        var fromEmail = marketRow?.FromEmail ?? config.Resend.From;
        var smsFrom = marketRow?.SmsFrom ?? config.Twilio.From;

        return new ResolvedSenderConfig(country, fromEmail, smsFrom);
    }
}
