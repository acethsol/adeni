namespace Adeni.Infrastructure.Persistence;

using Adeni.Application.Catalog;
using Adeni.Application.Storage;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Catalog;
using Adeni.Infrastructure.Markets;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Idempotent dev seed for the Beauty &amp; Wellness wedge (Lagos + Ottawa, enabled categories).
/// Skips slugs that already exist. Drop tenant data and restart the API to reseed from scratch.
/// </summary>
public static class DevelopmentDataSeeder
{
    public const string SeedMarkerSlug = "lekki-cuts";
    public const string DevBusinessAuth0Sub = "auth0|local-business";
    public const string DevCustomerAuth0Sub = "auth0|local-customer";
    public const string DevAdminAuth0Sub = "auth0|local-admin";

    private static readonly IReadOnlyDictionary<string, (string Currency, string TimeZoneId)> MarketDefaults =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["lagos"] = ("NGN", "Africa/Lagos"),
            ["ottawa"] = ("CAD", "America/Toronto"),
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
        var catalog = WellnessCategoryCatalogJson.ReadFromFile(new SeedHostEnvironment());

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
                Categories =
                [
                    new BusinessProfileCategory
                    {
                        TenantId = tenantId,
                        CategorySlug = sample.CategorySlug,
                        IsPrimary = true,
                    },
                ],
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

            AddServiceMenu(
                db,
                tenantId,
                sample.CategorySlug,
                marketDefaults.Currency,
                sample,
                catalog.GetServiceTemplates(sample.CategorySlug),
                [],
                now,
                added);

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
        var environment = new SeedHostEnvironment();
        await MarketCatalogSeeder.SeedIfEmptyAsync(db, environment, cancellationToken);
        await SeedSamplesAsync(db, cancellationToken);
        await EnsureServiceMenusAsync(db, environment, cancellationToken);
        await EnsureGalleryPhotosAsync(db, cancellationToken);
        await SeedDevBusinessOwnerAsync(db, cancellationToken);
        await SeedDevDepositSettingsAsync(db, cancellationToken);
        await SeedDevReviewFixtureAsync(db, cancellationToken);
    }

    private static async Task EnsureServiceMenusAsync(
        AdeniDbContext db,
        SeedHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var catalog = WellnessCategoryCatalogJson.ReadFromFile(environment);
        var businesses = await db.BusinessProfiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(profile => new { profile.TenantId, profile.CategorySlug })
            .ToListAsync(cancellationToken);

        var markets = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(location => location.IsPrimary)
            .Select(location => new { location.TenantId, location.MarketId })
            .ToListAsync(cancellationToken);
        var marketByTenant = markets.ToDictionary(x => x.TenantId, x => x.MarketId);

        var existing = await db.ServiceOfferings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(service => service.CatalogServiceId != null)
            .Select(service => new { service.TenantId, service.CatalogServiceId })
            .ToListAsync(cancellationToken);
        var owned = existing
            .Select(service => (service.TenantId, service.CatalogServiceId!))
            .ToHashSet();

        var now = DateTimeOffset.UtcNow;
        var touched = 0;
        foreach (var business in businesses)
        {
            if (!marketByTenant.TryGetValue(business.TenantId, out var marketId)
                || !MarketDefaults.TryGetValue(marketId, out var marketDefaults))
            {
                continue;
            }

            var added = AddServiceMenu(
                db,
                business.TenantId,
                business.CategorySlug,
                marketDefaults.Currency,
                sample: null,
                catalog.GetServiceTemplates(business.CategorySlug),
                owned,
                now,
                business.TenantId.GetHashCode());
            if (added == 0)
            {
                continue;
            }

            touched++;
            if (touched % 50 == 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        if (touched % 50 != 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task EnsureGalleryPhotosAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        var profiles = await db.BusinessProfiles
            .IgnoreQueryFilters()
            .Where(profile =>
                profile.GalleryImageKeysJson == null
                || profile.GalleryImageKeysJson == ""
                || profile.GalleryImageKeysJson.Contains("images.unsplash.com"))
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var touched = 0;
        foreach (var profile in profiles)
        {
            if (!GalleryPhotos.TryGetValue(profile.CategorySlug, out var photos) || photos.Length < 5)
            {
                continue;
            }

            var picked = PickPhotos(photos, profile.TenantId, 5);
            if (string.IsNullOrWhiteSpace(profile.CoverImageKey)
                || profile.CoverImageKey.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                profile.CoverImageKey = picked[0];
            }

            profile.GalleryImageKeysJson = GalleryImageKeys.Serialize(picked.Skip(1).Take(4).ToArray());
            profile.UpdatedAt = now;
            touched++;

            if (touched % 100 == 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        if (touched % 100 != 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string[] PickPhotos(string[] pool, Guid tenantId, int count)
    {
        var order = pool.ToArray();
        var bytes = tenantId.ToByteArray();
        var state = BitConverter.ToUInt32(bytes, 0)
            ^ BitConverter.ToUInt32(bytes, 4)
            ^ BitConverter.ToUInt32(bytes, 8);
        for (var i = order.Length - 1; i > 0; i--)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            var swap = (int)(state % (uint)(i + 1));
            (order[i], order[swap]) = (order[swap], order[i]);
        }

        return order.Take(count).ToArray();
    }

    private static string Photo(string id) =>
        $"https://images.unsplash.com/{id}?w=1200&auto=format&fit=crop&q=80";

    private static readonly Dictionary<string, string[]> GalleryPhotos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hair-grooming"] =
        [
            Photo("photo-1621605815971-fbc98d665033"),
            Photo("photo-1560066984-138dadb4c035"),
            Photo("photo-1503951914875-452162b0f3f1"),
            Photo("photo-1595476108010-b4d1f102b1b1"),
            Photo("photo-1562322140-8baeececf3df"),
            Photo("photo-1521590832167-7bcbfaa6381f"),
            Photo("photo-1605497788044-5a32c7078486"),
            Photo("photo-1492106087820-71f1a00d2b11"),
            Photo("photo-1519699047748-de8e457a634e"),
            Photo("photo-1580618672591-eb180b1a973f"),
            Photo("photo-1470259078422-826894b933aa"),
            Photo("photo-1622286342621-4bd786c2447c"),
            Photo("photo-1599351431202-1e0f0137899a"),
            Photo("photo-1516975080664-ed2fc6a32937"),
            Photo("photo-1500840216050-6ffa99d75160"),
            Photo("photo-1622287162716-f311baa1a2b8"),
            Photo("photo-1595475884562-073c30d45670"),
            Photo("photo-1600948836101-f9ffda59d250"),
            Photo("photo-1620331311520-246422fd82f9"),
            Photo("photo-1633681926022-84c23e8cb2d6"),
        ],
        ["massage-bodywork"] =
        [
            Photo("photo-1544161515-4ab6ce6db874"),
            Photo("photo-1600334129128-685c5582fd35"),
            Photo("photo-1519824145371-296894a0daa9"),
            Photo("photo-1591343395082-e120087004b4"),
            Photo("photo-1519823551278-64ac92734fb1"),
            Photo("photo-1600334089648-b0d9d3028eb2"),
            Photo("photo-1545205597-3d9d02c29597"),
            Photo("photo-1552196563-55cd4e45efb3"),
            Photo("photo-1506126613408-eca07ce68773"),
            Photo("photo-1540555700478-4be289fbecef"),
            Photo("photo-1515377905703-c4788e51af15"),
            Photo("photo-1571902943202-507ec2618e8f"),
            Photo("photo-1599447421416-3414500d18a5"),
            Photo("photo-1545389336-cf090694435e"),
            Photo("photo-1506126279646-a697353d3166"),
            Photo("photo-1575052814086-f385e2e2ad1b"),
        ],
        ["spa-relaxation"] =
        [
            Photo("photo-1540555700478-4be289fbecef"),
            Photo("photo-1515377905703-c4788e51af15"),
            Photo("photo-1571902943202-507ec2618e8f"),
            Photo("photo-1600334089648-b0d9d3028eb2"),
            Photo("photo-1552693673-1bf958298935"),
            Photo("photo-1544161515-4ab6ce6db874"),
            Photo("photo-1600334129128-685c5582fd35"),
            Photo("photo-1519824145371-296894a0daa9"),
            Photo("photo-1591343395082-e120087004b4"),
            Photo("photo-1519823551278-64ac92734fb1"),
            Photo("photo-1570172619644-dfd03ed5d881"),
            Photo("photo-1616394584738-fc6e612e71b9"),
            Photo("photo-1556228720-195a672e8a03"),
            Photo("photo-1598440947619-2c35fc9aa908"),
            Photo("photo-1612817288484-6f916006741a"),
            Photo("photo-1545205597-3d9d02c29597"),
        ],
        ["skincare-aesthetics"] =
        [
            Photo("photo-1570172619644-dfd03ed5d881"),
            Photo("photo-1616394584738-fc6e612e71b9"),
            Photo("photo-1512290923902-8a9f81dc236c"),
            Photo("photo-1487412947147-5cebf100ffc2"),
            Photo("photo-1522335789203-aabd1fc54bc9"),
            Photo("photo-1556228720-195a672e8a03"),
            Photo("photo-1598440947619-2c35fc9aa908"),
            Photo("photo-1612817288484-6f916006741a"),
            Photo("photo-1556228578-0d85b1a4d571"),
            Photo("photo-1596755389378-c31d21fd1273"),
            Photo("photo-1611930022073-b7a4ba5fcccd"),
            Photo("photo-1620916566398-39f1143ab7be"),
            Photo("photo-1571781926291-c477ebfd024b"),
            Photo("photo-1556228578-8c89e6adf883"),
            Photo("photo-1512496015851-a90fb38ba796"),
            Photo("photo-1596462502278-27bfdc403348"),
            Photo("photo-1526045478516-99145907023c"),
            Photo("photo-1515688594390-b649af70d282"),
            Photo("photo-1487412720507-e7ab37603c6f"),
            Photo("photo-1571290274554-6a2eaa771e5f"),
        ],
        ["nails"] =
        [
            Photo("photo-1604654894610-df63bc536371"),
            Photo("photo-1519014816548-bf5fe059798b"),
            Photo("photo-1632345031435-8727f6897d53"),
            Photo("photo-1610992015732-2449b76344bc"),
            Photo("photo-1457972729786-0411a3b2b626"),
            Photo("photo-1522337094846-8a818192de1f"),
            Photo("photo-1519415943484-9fa1873496d4"),
            Photo("photo-1522337660859-02fbefca4702"),
            Photo("photo-1583001931096-959e9a1a6223"),
            Photo("photo-1571290274554-6a2eaa771e5f"),
            Photo("photo-1596462502278-27bfdc403348"),
            Photo("photo-1526045478516-99145907023c"),
            Photo("photo-1512496015851-a90fb38ba796"),
            Photo("photo-1582095133179-bfd08e2fc6b3"),
            Photo("photo-1515688594390-b649af70d282"),
            Photo("photo-1487412720507-e7ab37603c6f"),
        ],
        ["fitness"] =
        [
            Photo("photo-1517836357463-d25dfeac3438"),
            Photo("photo-1534438327276-14e5300c3a48"),
            Photo("photo-1571019614242-c5c5dee9f50b"),
            Photo("photo-1518611012118-696072aa579a"),
            Photo("photo-1574680096145-d05b474e2155"),
            Photo("photo-1517963879433-6ad2b056d712"),
            Photo("photo-1534258936925-c58bed479fcb"),
            Photo("photo-1434682881908-b43d0467b798"),
            Photo("photo-1517838277536-f5f99be501cd"),
            Photo("photo-1581009146145-b5ef050c2e1e"),
            Photo("photo-1540497077202-7c8a3999166f"),
            Photo("photo-1576678927484-cc907957088c"),
            Photo("photo-1550345332-09e3ac987658"),
            Photo("photo-1571019613454-1cb2f99b2d8b"),
            Photo("photo-1518310383802-640c2de311b2"),
            Photo("photo-1476480862126-209bfaa8edc8"),
            Photo("photo-1599058917212-d750089bc07e"),
            Photo("photo-1434596922112-19c563067271"),
        ],
        ["yoga"] =
        [
            Photo("photo-1544367567-0f2fcb009e0b"),
            Photo("photo-1506126613408-eca07ce68773"),
            Photo("photo-1545389336-cf090694435e"),
            Photo("photo-1599901860904-17e6ed7083a0"),
            Photo("photo-1575052814086-f385e2e2ad1b"),
            Photo("photo-1545205597-3d9d02c29597"),
            Photo("photo-1506126279646-a697353d3166"),
            Photo("photo-1552196563-55cd4e45efb3"),
        ],
        ["pilates"] =
        [
            Photo("photo-1518611012118-696072aa579a"),
            Photo("photo-1599447421416-3414500d18a5"),
            Photo("photo-1571019613454-1cb2f99b2d8b"),
            Photo("photo-1518310383802-640c2de311b2"),
            Photo("photo-1476480862126-209bfaa8edc8"),
            Photo("photo-1550345332-09e3ac987658"),
            Photo("photo-1434596922112-19c563067271"),
            Photo("photo-1540497077202-7c8a3999166f"),
        ],
        ["yoga-pilates"] =
        [
            Photo("photo-1544367567-0f2fcb009e0b"),
            Photo("photo-1518611012118-696072aa579a"),
        ],
    };

    private static int AddServiceMenu(
        AdeniDbContext db,
        Guid tenantId,
        string categorySlug,
        string currency,
        DevelopmentSeedCatalog.SampleBusiness? sample,
        IReadOnlyList<ServiceTemplateResponse> templates,
        HashSet<(Guid TenantId, string CatalogServiceId)> owned,
        DateTimeOffset now,
        int salt)
    {
        var added = 0;
        if (templates.Count == 0)
        {
            if (sample is null)
            {
                return 0;
            }

            db.ServiceOfferings.Add(new ServiceOffering
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = sample.ServiceName,
                Description = sample.ServiceDescription,
                PriceAmount = sample.PriceAmount,
                Currency = currency,
                DurationMinutes = sample.DurationMinutes,
                CategorySlug = categorySlug,
                CatalogServiceId = sample.CatalogServiceId,
                BookingDeliveryType = sample.BookingDeliveryType,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            return 1;
        }

        for (var index = 0; index < templates.Count; index++)
        {
            var template = templates[index];
            if (!owned.Add((tenantId, template.Id)))
            {
                continue;
            }

            var isAnchor = sample?.CatalogServiceId == template.Id;
            db.ServiceOfferings.Add(new ServiceOffering
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = isAnchor ? sample!.ServiceName : template.Name,
                Description = isAnchor
                    ? sample!.ServiceDescription
                    : $"{template.Name}. Book a time online.",
                PriceAmount = isAnchor ? sample!.PriceAmount : MenuPrice(currency, index, salt),
                Currency = currency,
                DurationMinutes = isAnchor ? sample!.DurationMinutes : template.DefaultDurationMinutes,
                CategorySlug = categorySlug,
                CatalogServiceId = template.Id,
                BookingDeliveryType = isAnchor
                    ? sample!.BookingDeliveryType
                    : ParseDelivery(template.BookingDeliveryType),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            added++;
        }

        return added;
    }

    private static decimal MenuPrice(string currency, int index, int salt)
    {
        var (min, span) = currency switch
        {
            "NGN" => (6000m, 4500m),
            "CAD" => (30m, 16m),
            _ => (30m, 16m),
        };

        var step = (index + (salt & 7)) % 8;
        return decimal.Round(min + span * step, 0);
    }

    private static BookingDeliveryType ParseDelivery(string value) =>
        value.Replace("_", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant() switch
        {
            "class" => BookingDeliveryType.Class,
            "session" => BookingDeliveryType.Session,
            "experience" => BookingDeliveryType.Experience,
            "mobileappointment" => BookingDeliveryType.MobileAppointment,
            _ => BookingDeliveryType.Appointment,
        };

    private sealed class SeedHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Adeni";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
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

    private static async Task SeedDevReviewFixtureAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        var location = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Slug == SeedMarkerSlug, cancellationToken);

        if (location is null)
        {
            return;
        }

        var customer = await db.Customers
            .FirstOrDefaultAsync(entry => entry.Auth0Sub == DevCustomerAuth0Sub, cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                Auth0Sub = DevCustomerAuth0Sub,
                Name = "Local Customer",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync(cancellationToken);
        }

        var completedBookingIds = await db.Bookings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(booking =>
                booking.CustomerId == customer.Id
                && booking.TenantId == location.TenantId
                && booking.Status == BookingStatus.Confirmed
                && booking.EndAt <= DateTimeOffset.UtcNow)
            .Select(booking => booking.Id)
            .ToListAsync(cancellationToken);

        if (completedBookingIds.Count > 0)
        {
            var reviewedBookingIds = await db.Reviews
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(review => completedBookingIds.Contains(review.BookingId))
                .Select(review => review.BookingId)
                .ToListAsync(cancellationToken);

            if (completedBookingIds.Except(reviewedBookingIds).Any())
            {
                return;
            }
        }

        var service = await db.ServiceOfferings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entry => entry.TenantId == location.TenantId && entry.IsActive)
            .OrderBy(entry => entry.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (service is null)
        {
            return;
        }

        var startAt = DateTimeOffset.UtcNow.AddDays(-3);
        var endAt = startAt.AddMinutes(service.DurationMinutes);

        db.Bookings.Add(new BookingRecord
        {
            Id = Guid.NewGuid(),
            TenantId = location.TenantId,
            ServiceOfferingId = service.Id,
            CustomerId = customer.Id,
            StartAt = startAt,
            EndAt = endAt,
            Status = BookingStatus.Confirmed,
            CustomerNotes = "Dev seed — ready for review E2E",
            CreatedAt = startAt,
            UpdatedAt = startAt,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDevDepositSettingsAsync(
        AdeniDbContext db,
        CancellationToken cancellationToken)
    {
        var location = await db.BusinessLocations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Slug == SeedMarkerSlug, cancellationToken);

        if (location is null)
        {
            return;
        }

        var profile = await db.BusinessProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(entry => entry.TenantId == location.TenantId, cancellationToken);

        if (profile is null || profile.DepositPercent > 0)
        {
            return;
        }

        profile.DepositPercent = 30;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
