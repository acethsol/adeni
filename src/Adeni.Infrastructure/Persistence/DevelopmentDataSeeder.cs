namespace Adeni.Infrastructure.Persistence;

using Adeni.Application.Catalog;
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
