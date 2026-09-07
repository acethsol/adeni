namespace Adeni.Infrastructure.Discovery;

using Adeni.Domain.Booking;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

internal static class DiscoveryTrustSignals
{
    private static readonly Dictionary<string, string[]> CategoryRequiredBadges =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["plumbers"] = ["license"],
            ["electricians"] = ["license"],
        };

    public static bool MeetsCategoryBadgeRequirements(
        string categorySlug,
        IReadOnlyList<string>? badges)
    {
        if (!CategoryRequiredBadges.TryGetValue(categorySlug, out var required))
        {
            return true;
        }

        if (badges is null || badges.Count == 0)
        {
            return false;
        }

        return required.All(requiredBadge =>
            badges.Contains(requiredBadge, StringComparer.OrdinalIgnoreCase));
    }

    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> LoadBadgesAsync(
        AdeniDbContext dbContext,
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken)
    {
        if (tenantIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var rows = await dbContext.TenantVerificationBadges
            .AsNoTracking()
            .Where(x => tenantIds.Contains(x.TenantId) && x.Status == VerificationBadgeStatus.Granted)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.TenantId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(x => ToApiBadgeType(x.BadgeType))
                    .Distinct()
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList());
    }

    private static string ToApiBadgeType(VerificationBadgeType badgeType) =>
        badgeType switch
        {
            VerificationBadgeType.Phone => "phone",
            VerificationBadgeType.Cac => "cac",
            VerificationBadgeType.Address => "address",
            VerificationBadgeType.License => "license",
            _ => badgeType.ToString().ToLowerInvariant(),
        };

    public static async Task<IReadOnlyDictionary<Guid, double?>> LoadCompletionRatesAsync(
        AdeniDbContext dbContext,
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken)
    {
        if (tenantIds.Count == 0)
        {
            return new Dictionary<Guid, double?>();
        }

        var now = DateTimeOffset.UtcNow;
        var rows = await dbContext.Bookings
            .AsNoTracking()
            .Where(x => tenantIds.Contains(x.TenantId) && x.Status == BookingStatus.Confirmed)
            .GroupBy(x => x.TenantId)
            .Select(g => new
            {
                TenantId = g.Key,
                Total = g.Count(),
                Completed = g.Count(x => x.EndAt <= now),
            })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, double?>(rows.Count);
        foreach (var row in rows)
        {
            result[row.TenantId] = row.Total == 0
                ? null
                : Math.Round((double)row.Completed / row.Total, 2);
        }

        return result;
    }
}
