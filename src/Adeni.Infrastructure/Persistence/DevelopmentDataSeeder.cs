namespace Adeni.Infrastructure.Persistence;

using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Idempotent dev seed — ~1,000 verified businesses across all markets (Lagos-heavy).
/// Skips slugs that already exist so new samples can be appended without a DB reset.
/// </summary>
public static class DevelopmentDataSeeder
{
    public const string SeedMarkerSlug = "lekki-cuts";
    public const string DevBusinessAuth0Sub = "auth0|local-business";

    private static readonly IReadOnlyDictionary<string, (string Currency, string TimeZoneId)> MarketDefaults =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["lagos"] = ("NGN", "Africa/Lagos"),
            ["abuja"] = ("NGN", "Africa/Lagos"),
            ["ottawa"] = ("CAD", "America/Toronto"),
            ["toronto"] = ("CAD", "America/Toronto"),
            ["houston"] = ("USD", "America/Chicago"),
            ["dallas"] = ("USD", "America/Chicago"),
        };

    private static async Task SeedSamplesAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        var existingSlugs = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(x => x.Slug)
            .ToListAsync(cancellationToken);

        var slugSet = existingSlugs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;
        var added = 0;
        const int saveBatchSize = 100;

        foreach (var sample in DevelopmentSeedCatalog.All)
        {
            if (slugSet.Contains(sample.Slug))
            {
                continue;
            }

            if (!MarketDefaults.TryGetValue(sample.MarketId, out var marketDefaults))
            {
                continue;
            }

            var tenantId = Guid.NewGuid();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = sample.Name,
                Status = TenantStatus.Verified,
                CreatedAt = now,
                VerifiedAt = now,
            });

            db.BusinessProfiles.Add(new BusinessProfile
            {
                TenantId = tenantId,
                CategorySlug = sample.CategorySlug,
                Phone = sample.Phone,
                Description = sample.Description,
                UpdatedAt = now,
            });

            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Slug = sample.Slug,
                Name = sample.LocationName,
                MarketId = sample.MarketId,
                AddressLine = sample.AddressLine,
                Area = sample.Area,
                Latitude = sample.Latitude,
                Longitude = sample.Longitude,
                TimeZoneId = marketDefaults.TimeZoneId,
                IsPrimary = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });

            db.ServiceOfferings.Add(new ServiceOffering
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = sample.ServiceName,
                Description = sample.ServiceDescription,
                PriceAmount = sample.PriceAmount,
                Currency = marketDefaults.Currency,
                DurationMinutes = sample.DurationMinutes,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });

            foreach (var day in new[]
                     {
                         DayOfWeek.Monday,
                         DayOfWeek.Tuesday,
                         DayOfWeek.Wednesday,
                         DayOfWeek.Thursday,
                         DayOfWeek.Friday,
                         DayOfWeek.Saturday,
                     })
            {
                db.WeeklyAvailabilities.Add(new WeeklyAvailability
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    DayOfWeek = day,
                    OpenTime = new TimeOnly(9, 0),
                    CloseTime = new TimeOnly(17, 0),
                });
            }

            slugSet.Add(sample.Slug);
            added++;

            if (added % saveBatchSize == 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        if (added % saveBatchSize != 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static async Task SeedAsync(AdeniDbContext db, CancellationToken cancellationToken = default)
    {
        await SeedSamplesAsync(db, cancellationToken);
        await SeedDevBusinessOwnerAsync(db, cancellationToken);
    }

    private static async Task SeedDevBusinessOwnerAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        if (await db.BusinessUsers
                .IgnoreQueryFilters()
                .AnyAsync(user => user.Auth0Sub == DevBusinessAuth0Sub, cancellationToken))
        {
            return;
        }

        var location = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entry => entry.Slug == SeedMarkerSlug,
                cancellationToken);

        if (location is null)
        {
            return;
        }

        db.BusinessUsers.Add(new BusinessUser
        {
            Id = Guid.NewGuid(),
            TenantId = location.TenantId,
            Auth0Sub = DevBusinessAuth0Sub,
            Role = "owner",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
