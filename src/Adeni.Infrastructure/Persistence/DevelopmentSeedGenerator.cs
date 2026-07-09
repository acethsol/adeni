namespace Adeni.Infrastructure.Persistence;

/// <summary>Deterministic bulk dev businesses — complements handcrafted anchors in <see cref="DevelopmentSeedCatalog"/>.</summary>
internal static class DevelopmentSeedGenerator
{
    internal const int TargetBulkCount = 963;

    private static readonly string[] CategorySlugs =
    [
        "barbers",
        "hair-salons",
        "nail-spa",
        "makeup-brows",
        "plumbers",
        "electricians",
        "cleaning",
    ];

    private static readonly MarketBulkConfig[] MarketConfigs =
    [
        new(
            "lagos",
            463,
            6.5244,
            3.3792,
            "+23480",
            "NGN",
            [
                "Lekki", "Victoria Island", "Ikeja", "Yaba", "Surulere", "Ajah", "Maryland", "Ikoyi",
                "Gbagada", "Festac", "Mushin", "Apapa", "Berger", "Ogba", "Magodo", "Banana Island",
                "Ogudu", "Alimosho", "Egbeda", "Badagry",
            ]),
        new("abuja", 100, 9.0765, 7.3986, "+23480", "NGN",
            ["Wuse", "Garki", "Maitama", "Gwarinpa", "Kubwa", "Jabi", "Asokoro", "Utako", "Nyanya", "Lugbe"]),
        new("ottawa", 100, 45.4215, -75.6972, "+1613", "CAD",
            ["Centretown", "ByWard Market", "The Glebe", "Westboro", "Kanata", "Orleans", "Nepean", "Barrhaven", "Hintonburg", "Vanier"]),
        new("toronto", 100, 43.6532, -79.3832, "+1416", "CAD",
            ["The Annex", "Queen West", "Yorkville", "Kensington", "Leslieville", "Scarborough", "North York", "Etobicoke", "Liberty Village", "Distillery"]),
        new("houston", 100, 29.7604, -95.3698, "+1713", "USD",
            ["Montrose", "The Heights", "Midtown", "Rice Village", "Uptown", "Memorial", "Katy", "Sugar Land", "Pearland", "Spring"]),
        new("dallas", 100, 32.7767, -96.7970, "+1214", "USD",
            ["Deep Ellum", "Uptown", "Bishop Arts", "Preston Hollow", "Frisco", "Plano", "Oak Lawn", "Lower Greenville", "Lakewood", "Design District"]),
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
            $"{name} — verified local {FormatCategory(categorySlug)} in {area}. Book online.",
            service.Name,
            service.Description,
            price,
            service.DurationMinutes);
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
        var (min, max) = currency switch
        {
            "NGN" => categorySlug is "plumbers" or "electricians" or "cleaning"
                ? (15000m, 85000m)
                : (5000m, 45000m),
            "CAD" => categorySlug is "plumbers" or "electricians" or "cleaning"
                ? (95m, 320m)
                : (25m, 180m),
            _ => categorySlug is "plumbers" or "electricians" or "cleaning"
                ? (75m, 280m)
                : (25m, 200m),
        };

        var step = (max - min) / 7;
        return min + step * (index % 8);
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

    private sealed record ServiceTemplate(string Name, string Description, int DurationMinutes);

    private static readonly Dictionary<string, string[]> CategorySuffixes = new(StringComparer.Ordinal)
    {
        ["barbers"] = ["Cuts", "Barber Co", "Fade Factory", "Grooming", "Clipper House", "Line Up", "Chair & Blade"],
        ["hair-salons"] = ["Hair Studio", "Salon", "Hair Lounge", "Braids Co", "Colour House", "Silk Press", "Natural Hair"],
        ["nail-spa"] = ["Nail Lounge", "Nail Spa", "Polish Bar", "Spa Day", "Nail House", "Glow Nails", "Mani Pedi"],
        ["makeup-brows"] = ["Beauty Bar", "Brow Studio", "Glam Studio", "Lash Lounge", "Makeup Co", "Brow Bar", "Glow Bar"],
        ["plumbers"] = ["Plumbing", "Pipe Pros", "Drain Fix", "Aqua Service", "Flow Masters", "Leak Patrol", "Pipe Works"],
        ["electricians"] = ["Electric", "Wire Pros", "Power Fix", "Volt Service", "Circuit Co", "Spark Works", "Bright Wire"],
        ["cleaning"] = ["Cleaning Co", "Fresh Home", "Sparkle Crew", "Clean Team", "Tidy Pros", "Shine Service", "Home Care"],
    };

    private static readonly Dictionary<string, ServiceTemplate[]> CategoryServices = new(StringComparer.Ordinal)
    {
        ["barbers"] =
        [
            new("Classic haircut", "Clippers, line-up, and neck finish.", 30),
            new("Skin fade", "Skin fade with beard sculpt.", 45),
            new("Beard trim", "Shape, line, and hot towel.", 20),
        ],
        ["hair-salons"] =
        [
            new("Silk press", "Wash, blow-dry, and silk press.", 90),
            new("Knotless braids", "Medium length protective style.", 180),
            new("Cut & blowout", "Consultation, cut, and styled finish.", 60),
        ],
        ["nail-spa"] =
        [
            new("Gel manicure", "Shape, cuticle care, and gel polish.", 60),
            new("Spa pedicure", "Soak, scrub, massage, and polish.", 75),
            new("Luxury manicure", "Full manicure with hand massage.", 55),
        ],
        ["makeup-brows"] =
        [
            new("Brow shaping", "Thread or wax with optional tint.", 30),
            new("Soft glam makeup", "Full face for events.", 60),
            new("Lash lift", "Lift and tint.", 45),
        ],
        ["plumbers"] =
        [
            new("Leak repair", "Diagnose and fix common leaks.", 90),
            new("Drain clearing", "Clear blocked sink or shower drain.", 60),
            new("Tap installation", "Replace or install kitchen/bath tap.", 75),
        ],
        ["electricians"] =
        [
            new("Outlet repair", "Fix or replace faulty outlet.", 60),
            new("Light fitting", "Install ceiling or wall light.", 75),
            new("Fault finding", "Trace and fix electrical fault.", 90),
        ],
        ["cleaning"] =
        [
            new("Standard clean", "Kitchen, bath, and living areas.", 120),
            new("Deep clean", "Detailed clean including appliances.", 180),
            new("Move-out clean", "Full apartment turnover clean.", 240),
        ],
    };
}
