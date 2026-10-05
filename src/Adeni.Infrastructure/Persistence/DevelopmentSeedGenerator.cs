namespace Adeni.Infrastructure.Persistence;

using Adeni.Domain.Booking;

/// <summary>
/// Deterministic bulk dev businesses for the Beauty &amp; Wellness wedge.
/// Lagos and Ottawa only — complements handcrafted anchors in <see cref="DevelopmentSeedCatalog"/>.
/// </summary>
internal static class DevelopmentSeedGenerator
{
    internal const int LagosBulkCount = 126;

    internal const int OttawaBulkCount = 56;

    internal const int TargetBulkCount = LagosBulkCount + OttawaBulkCount;

    internal static readonly string[] CategorySlugs =
    [
        "hair-grooming",
        "nails",
        "skincare-aesthetics",
        "spa-relaxation",
        "massage-bodywork",
        "fitness",
        "yoga-pilates",
    ];

    private static readonly MarketBulkConfig[] MarketConfigs =
    [
        new(
            "lagos",
            LagosBulkCount,
            6.5244,
            3.3792,
            "+23480",
            "NGN",
            [
                "Lekki", "Victoria Island", "Ikeja", "Yaba", "Surulere", "Ajah", "Maryland", "Ikoyi",
                "Gbagada", "Festac", "Magodo", "Ogudu",
            ]),
        new(
            "ottawa",
            OttawaBulkCount,
            45.4215,
            -75.6972,
            "+1613",
            "CAD",
            [
                "Centretown", "ByWard Market", "The Glebe", "Westboro", "Kanata", "Orleans",
                "Hintonburg", "Vanier",
            ]),
    ];

    internal static IEnumerable<DevelopmentSeedCatalog.SampleBusiness> GenerateBulk()
    {
        var results = new List<DevelopmentSeedCatalog.SampleBusiness>(TargetBulkCount);
        var globalIndex = 0;

        foreach (var market in MarketConfigs)
        {
            for (var i = 0; i < market.Count; i++)
            {
                globalIndex++;
                results.Add(Build(market, globalIndex, i));
            }
        }

        return results;
    }

    private static DevelopmentSeedCatalog.SampleBusiness Build(MarketBulkConfig market, int globalIndex, int marketIndex)
    {
        var categorySlug = CategorySlugs[globalIndex % CategorySlugs.Length];
        var area = market.Areas[marketIndex % market.Areas.Length];
        var suffix = Pick(CategorySuffixes[categorySlug], globalIndex);
        var name = $"{area} {suffix}";
        var slug = $"{market.MarketId}-seed-{categorySlug}-{marketIndex + 1:D4}";
        var (lat, lng) = ScatterCoordinates(market.CenterLat, market.CenterLng, globalIndex);
        var service = PickService(CategoryServices[categorySlug], globalIndex);
        var price = ServicePrice(market.Currency, categorySlug, globalIndex);
        var phoneDigits = (1000000 + globalIndex).ToString();
        var phone = $"{market.PhonePrefix}{phoneDigits}";

        return new DevelopmentSeedCatalog.SampleBusiness(
            slug,
            name,
            area,
            market.MarketId,
            categorySlug,
            area,
            $"{10 + (globalIndex % 200)} {area} Street, {TitleCase(market.MarketId)}",
            lat,
            lng,
            phone,
            $"{name} — verified {FormatCategory(categorySlug)} in {area}. Book online.",
            service.Name,
            service.Description,
            price,
            service.DurationMinutes,
            service.CatalogServiceId,
            service.DeliveryType);
    }

    private static (double Lat, double Lng) ScatterCoordinates(double centerLat, double centerLng, int index)
    {
        var angle = index * 137.508 * (Math.PI / 180);
        var radiusKm = 0.4 + (index % 45) * 0.65;
        var lat = centerLat + (radiusKm * Math.Cos(angle)) / 111.0;
        var lng = centerLng + (radiusKm * Math.Sin(angle)) / (111.0 * Math.Cos(centerLat * Math.PI / 180));
        return (Math.Round(lat, 4), Math.Round(lng, 4));
    }

    private static decimal ServicePrice(string currency, string categorySlug, int index)
    {
        var (min, max) = (currency, categorySlug) switch
        {
            ("NGN", "spa-relaxation") => (18000m, 55000m),
            ("NGN", "massage-bodywork") => (12000m, 40000m),
            ("NGN", "skincare-aesthetics") => (9000m, 35000m),
            ("NGN", "hair-grooming") => (6000m, 35000m),
            ("NGN", "nails") => (8000m, 22000m),
            ("NGN", "fitness") => (8000m, 25000m),
            ("NGN", "yoga-pilates") => (5000m, 18000m),
            ("CAD", "spa-relaxation") => (90m, 180m),
            ("CAD", "massage-bodywork") => (75m, 150m),
            ("CAD", "skincare-aesthetics") => (45m, 140m),
            ("CAD", "hair-grooming") => (30m, 120m),
            ("CAD", "nails") => (35m, 90m),
            ("CAD", "fitness") => (40m, 100m),
            ("CAD", "yoga-pilates") => (22m, 80m),
            _ => (25m, 120m),
        };

        var step = (max - min) / 7;
        return decimal.Round(min + step * (index % 8), 0);
    }

    private static string Pick(string[] options, int index) => options[index % options.Length];

    private static ServiceTemplate PickService(ServiceTemplate[] options, int index) =>
        options[index % options.Length];

    private static string FormatCategory(string slug) =>
        slug.Replace('-', ' ');

    private static string TitleCase(string value) =>
        char.ToUpperInvariant(value[0]) + value[1..];

    private sealed record MarketBulkConfig(
        string MarketId,
        int Count,
        double CenterLat,
        double CenterLng,
        string PhonePrefix,
        string Currency,
        string[] Areas);

    private sealed record ServiceTemplate(
        string CatalogServiceId,
        string Name,
        string Description,
        int DurationMinutes,
        BookingDeliveryType DeliveryType);

    private static readonly Dictionary<string, string[]> CategorySuffixes = new(StringComparer.Ordinal)
    {
        ["hair-grooming"] = ["Cuts", "Grooming", "Hair Studio", "Braids Co", "Colour House", "Barber Co"],
        ["nails"] = ["Nail Lounge", "Nail Bar", "Polish Studio", "Nail House"],
        ["skincare-aesthetics"] = ["Skin Studio", "Brow Bar", "Glow Studio", "Aesthetics"],
        ["spa-relaxation"] = ["Day Spa", "Spa House", "Relaxation Studio"],
        ["massage-bodywork"] = ["Massage Studio", "Bodywork", "Massage Co"],
        ["fitness"] = ["Fitness Lab", "Strength Co", "Training Studio"],
        ["yoga-pilates"] = ["Yoga Loft", "Pilates Studio", "Movement Studio"],
    };

    private static readonly Dictionary<string, ServiceTemplate[]> CategoryServices = new(StringComparer.Ordinal)
    {
        ["hair-grooming"] =
        [
            new("hair-barbering", "Barbering", "Clipper cut, line-up, and neck finish.", 30, BookingDeliveryType.Appointment),
            new("hair-cut", "Haircut", "Consultation, cut, and style.", 45, BookingDeliveryType.Appointment),
            new("hair-braids", "Braids", "Medium-length protective style.", 120, BookingDeliveryType.Appointment),
            new("hair-beard", "Beard Grooming", "Shape, line, and hot towel.", 20, BookingDeliveryType.Appointment),
        ],
        ["nails"] =
        [
            new("nails-gel", "Gel Nails", "Shape, cuticle care, and gel polish.", 75, BookingDeliveryType.Appointment),
            new("nails-manicure", "Manicure", "Classic manicure with polish.", 45, BookingDeliveryType.Appointment),
            new("nails-pedicure", "Pedicure", "Soak, scrub, and polish.", 60, BookingDeliveryType.Appointment),
        ],
        ["skincare-aesthetics"] =
        [
            new("skin-facial", "Facial", "Cleanse, treatment, and moisturize.", 60, BookingDeliveryType.Appointment),
            new("skin-brows-lashes", "Brows & Lashes", "Shape, tint, and lash lift.", 45, BookingDeliveryType.Appointment),
            new("skin-waxing", "Waxing", "Brow or face wax.", 30, BookingDeliveryType.Appointment),
        ],
        ["spa-relaxation"] =
        [
            new("spa-body-treatment", "Body Treatment", "Scrub and massage treatment.", 60, BookingDeliveryType.Appointment),
            new("spa-day-package", "Day Spa Package", "Half-day spa with treatment and rest.", 120, BookingDeliveryType.Experience),
        ],
        ["massage-bodywork"] =
        [
            new("massage-swedish", "Swedish Massage", "Full-body relaxation massage.", 60, BookingDeliveryType.Appointment),
            new("massage-deep-tissue", "Deep Tissue Massage", "Focused pressure for tight muscles.", 60, BookingDeliveryType.Appointment),
            new("massage-sports", "Sports Massage", "Pre- or post-training bodywork.", 45, BookingDeliveryType.Appointment),
        ],
        ["fitness"] =
        [
            new("fitness-pt", "Personal Training", "One-to-one strength session.", 60, BookingDeliveryType.Session),
            new("fitness-strength", "Strength & Conditioning", "Coached strength session.", 45, BookingDeliveryType.Session),
        ],
        ["yoga-pilates"] =
        [
            new("yoga-group-class", "Yoga Class", "Group yoga class.", 60, BookingDeliveryType.Class),
            new("pilates-group-class", "Pilates Class", "Group mat Pilates.", 55, BookingDeliveryType.Class),
            new("yoga-private", "Private Yoga Session", "One-to-one yoga.", 60, BookingDeliveryType.Session),
        ],
    };
}
