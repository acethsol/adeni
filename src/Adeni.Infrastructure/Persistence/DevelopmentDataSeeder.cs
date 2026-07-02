namespace Adeni.Infrastructure.Persistence;

using Adeni.Domain.Booking;
using Adeni.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Idempotent dev seed — six verified businesses across NG / CA / US for discovery and booking tests.
/// </summary>
public static class DevelopmentDataSeeder
{
    public const string SeedMarkerSlug = "lekki-cuts";

    public static async Task SeedAsync(AdeniDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.BusinessLocations.AsNoTracking().AnyAsync(
                x => x.Slug == SeedMarkerSlug,
                cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var sample in Samples)
        {
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
                TimeZoneId = sample.TimeZoneId,
                IsPrimary = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });

            var serviceId = Guid.NewGuid();
            db.ServiceOfferings.Add(new ServiceOffering
            {
                Id = serviceId,
                TenantId = tenantId,
                Name = sample.ServiceName,
                Description = sample.ServiceDescription,
                PriceAmount = sample.PriceAmount,
                Currency = sample.Currency,
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
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record SampleBusiness(
        string Slug,
        string Name,
        string LocationName,
        string MarketId,
        string CategorySlug,
        string Area,
        string AddressLine,
        double Latitude,
        double Longitude,
        string TimeZoneId,
        string Phone,
        string Description,
        string ServiceName,
        string ServiceDescription,
        decimal PriceAmount,
        string Currency,
        int DurationMinutes);

    private static readonly SampleBusiness[] Samples =
    [
        new(
            "lekki-cuts",
            "Lekki Cuts",
            "Lekki",
            "lagos",
            "barbers",
            "Lekki Phase 1",
            "12 Admiralty Way, Lekki",
            6.4474,
            3.4700,
            "Africa/Lagos",
            "+2348012345678",
            "Walk-in fades and beard trims in Lekki. Book online — no long waits.",
            "Classic haircut",
            "Clippers, line-up, and hot towel finish.",
            8000m,
            "NGN",
            30),
        new(
            "abuja-glow-salon",
            "Abuja Glow Salon",
            "Wuse",
            "abuja",
            "hair-salons",
            "Wuse 2",
            "44 Adetokunbo Ademola Crescent, Wuse 2",
            9.0765,
            7.3986,
            "Africa/Lagos",
            "+2348098765432",
            "Braids, silk press, and natural hair care in the capital.",
            "Wash & blowout",
            "Shampoo, deep condition, and styled blowout.",
            15000m,
            "NGN",
            60),
        new(
            "ottawa-capitol-cuts",
            "Capitol Cuts",
            "Centretown",
            "ottawa",
            "barbers",
            "Centretown",
            "120 Bank Street, Ottawa",
            45.4215,
            -75.6972,
            "America/Toronto",
            "+16135550101",
            "Neighbourhood barbershop steps from Parliament Hill.",
            "Men's haircut",
            "Scissor or clipper cut with neck shave.",
            35m,
            "CAD",
            30),
        new(
            "toronto-annex-salon",
            "Annex Hair Studio",
            "The Annex",
            "toronto",
            "hair-salons",
            "The Annex",
            "402 Bloor Street West, Toronto",
            43.6677,
            -79.4068,
            "America/Toronto",
            "+14165550102",
            "Colour, cuts, and styling near the University of Toronto.",
            "Cut & style",
            "Consultation, cut, and finish.",
            85m,
            "CAD",
            45),
        new(
            "houston-montrose-barber",
            "Montrose Barber Co.",
            "Montrose",
            "houston",
            "barbers",
            "Montrose",
            "888 Westheimer Road, Houston",
            29.7420,
            -95.4010,
            "America/Chicago",
            "+17135550103",
            "Classic barbering in Montrose — hot lather shaves and fades.",
            "Skin fade",
            "Skin fade with beard trim.",
            45m,
            "USD",
            45),
        new(
            "dallas-deep-ellum-cuts",
            "Deep Ellum Cuts",
            "Deep Ellum",
            "dallas",
            "barbers",
            "Deep Ellum",
            "2800 Main Street, Dallas",
            32.7831,
            -96.7844,
            "America/Chicago",
            "+12145550104",
            "Modern cuts in Deep Ellum. Walk-ins welcome, booking preferred.",
            "Signature cut",
            "Cut, wash, and style.",
            40m,
            "USD",
            30),
    ];
}
