namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Markets;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class TenantSchedulingTimeZone(
    AdeniDbContext dbContext,
    IOptions<MarketOptions> marketOptions,
    ILogger<TenantSchedulingTimeZone> logger) : ITenantSchedulingTimeZone
{
    public async Task<ISchedulingTimeZone> ForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var locationTimeZone = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && x.IsPrimary)
            .Select(x => x.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        return ResolveTimeZone(locationTimeZone);
    }

    public async Task<ISchedulingTimeZone> ForLocationAsync(
        Guid locationId,
        CancellationToken cancellationToken = default)
    {
        var locationTimeZone = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.Id == locationId && x.IsActive)
            .Select(x => x.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        return ResolveTimeZone(locationTimeZone);
    }

    private ISchedulingTimeZone ResolveTimeZone(string? locationTimeZone)
    {
        var timeZoneId = string.IsNullOrWhiteSpace(locationTimeZone)
            ? marketOptions.Value.DefaultTimeZoneId
            : locationTimeZone;

        return SchedulingTimeZone.FromId(timeZoneId, logger);
    }
}
