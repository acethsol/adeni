namespace Adeni.Infrastructure.Catalog;

using System.Text.Json;
using Adeni.Application.Catalog;
using Microsoft.Extensions.Hosting;

internal sealed class WellnessCategoryCatalogJson : IWellnessCategoryCatalog
{
    private readonly Dictionary<string, WellnessCategoryEntry> _bySlug;
    private readonly Dictionary<string, string> _legacyAliases;
    private readonly Dictionary<string, List<string>> _aliasesByCanonical;
    private readonly Dictionary<string, IReadOnlyList<ServiceTemplateResponse>> _templatesByCategory;
    private readonly Dictionary<string, List<string>> _marketFeaturedOrder;

    private WellnessCategoryCatalogJson(
        IReadOnlyList<WellnessCategoryEntry> categories,
        Dictionary<string, string> legacyAliases,
        Dictionary<string, List<string>> marketFeaturedOrder,
        Dictionary<string, List<WellnessServiceTemplateEntry>> serviceTemplates)
    {
        _bySlug = categories.ToDictionary(c => c.Slug, c => c, StringComparer.OrdinalIgnoreCase);
        _legacyAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in legacyAliases)
        {
            _legacyAliases[pair.Key.Trim()] = pair.Value.Trim().ToLowerInvariant();
        }

        _aliasesByCanonical = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _legacyAliases)
        {
            if (!_aliasesByCanonical.TryGetValue(pair.Value, out var list))
            {
                list = [];
                _aliasesByCanonical[pair.Value] = list;
            }

            list.Add(pair.Key.ToLowerInvariant());
        }

        _marketFeaturedOrder = marketFeaturedOrder;
        _templatesByCategory = BuildServiceTemplates(serviceTemplates);
    }

    internal static WellnessCategoryCatalogJson ReadFromFile(IHostEnvironment environment)
    {
        var path = ResolveCatalogPath(environment, "wellness-categories.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Wellness category catalog not found at '{path}'. Ensure packages/shared/src/data/wellness-categories.json is copied to output.");
        }

        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<WellnessCategoriesFile>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Wellness category catalog JSON is empty or invalid.");

        var templates = new Dictionary<string, List<WellnessServiceTemplateEntry>>(StringComparer.OrdinalIgnoreCase);
        var templatesPath = ResolveCatalogPath(environment, "wellness-service-templates.json");
        if (File.Exists(templatesPath))
        {
            var templatesJson = File.ReadAllText(templatesPath);
            var templatesDoc = JsonSerializer.Deserialize<WellnessServiceTemplatesFile>(templatesJson, SerializerOptions);
            if (templatesDoc?.TemplatesByCategory is not null)
            {
                templates = templatesDoc.TemplatesByCategory;
            }
        }

        return new WellnessCategoryCatalogJson(
            document.Categories,
            document.LegacyAliases,
            document.MarketFeaturedOrder,
            templates);
    }

    public IReadOnlyList<CategoryResponse> ListCategories(CategoryListQuery query)
    {
        IEnumerable<WellnessCategoryEntry> entries = _bySlug.Values;

        if (query.WellnessScope)
        {
            entries = entries.Where(c => c.WellnessScope != false);
        }

        if (!query.IncludeDisabled)
        {
            entries = entries.Where(c => c.Enabled);
        }

        var list = entries
            .Select(c => new CategoryResponse(
                Guid.Parse(c.Id),
                c.Name,
                c.Slug,
                c.ParentSlug))
            .ToList();

        var order = ResolveMarketOrder(query.MarketId);
        if (order.Count > 0)
        {
            var orderIndex = order
                .Select((slug, index) => (slug, index))
                .ToDictionary(x => x.slug, x => x.index, StringComparer.OrdinalIgnoreCase);
            list = list
                .OrderBy(c =>
                {
                    return orderIndex.TryGetValue(c.Slug, out var index) ? index : int.MaxValue;
                })
                .ThenBy(c => c.Name, StringComparer.Ordinal)
                .ToList();
        }
        else
        {
            list = list.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
        }

        return list;
    }

    public bool IsKnownSlug(string slug)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return _bySlug.ContainsKey(normalized) || _legacyAliases.ContainsKey(normalized);
    }

    public string NormalizeSlug(string slug)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        if (_legacyAliases.TryGetValue(normalized, out var canonical))
        {
            return canonical;
        }

        return normalized;
    }

    public IReadOnlyList<string> GetDiscoveryMatchSlugs(string filterSlug)
    {
        var canonical = NormalizeSlug(filterSlug);
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { canonical };
        if (_aliasesByCanonical.TryGetValue(canonical, out var aliases))
        {
            foreach (var alias in aliases)
            {
                slugs.Add(alias);
            }
        }

        return slugs.ToList();
    }

    public IReadOnlyList<ServiceTemplateResponse> GetServiceTemplates(string categorySlug)
    {
        var canonical = NormalizeSlug(categorySlug);
        return _templatesByCategory.TryGetValue(canonical, out var templates)
            ? templates
            : [];
    }

    private IReadOnlyList<string> ResolveMarketOrder(string? marketId)
    {
        if (string.IsNullOrWhiteSpace(marketId))
        {
            return _marketFeaturedOrder.TryGetValue("default", out var fallback)
                ? fallback
                : [];
        }

        var key = marketId.Trim().ToLowerInvariant();
        if (_marketFeaturedOrder.TryGetValue(key, out var order))
        {
            return order;
        }

        return _marketFeaturedOrder.TryGetValue("default", out var def) ? def : [];
    }

    private Dictionary<string, IReadOnlyList<ServiceTemplateResponse>> BuildServiceTemplates(
        Dictionary<string, List<WellnessServiceTemplateEntry>> serviceTemplates)
    {
        var result = new Dictionary<string, IReadOnlyList<ServiceTemplateResponse>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in serviceTemplates)
        {
            var canonical = NormalizeSlug(pair.Key);
            result[canonical] = pair.Value
                .Select(t => new ServiceTemplateResponse(
                    t.Id,
                    t.Name,
                    t.DefaultDurationMinutes,
                    t.BookingDeliveryType))
                .ToList();
        }

        return result;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string ResolveCatalogPath(IHostEnvironment environment, string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(environment.ContentRootPath, "Catalog", fileName),
            Path.Combine(AppContext.BaseDirectory, "Catalog", fileName),
            Path.Combine(AppContext.BaseDirectory, fileName),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    private sealed class WellnessCategoriesFile
    {
        public List<WellnessCategoryEntry> Categories { get; init; } = [];

        public Dictionary<string, string> LegacyAliases { get; init; } = [];

        public Dictionary<string, List<string>> MarketFeaturedOrder { get; init; } = [];
    }

    private sealed class WellnessCategoryEntry
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public required string Slug { get; init; }

        public string? ParentSlug { get; init; }

        public bool Enabled { get; init; }

        public bool? WellnessScope { get; init; }
    }

    private sealed class WellnessServiceTemplatesFile
    {
        public Dictionary<string, List<WellnessServiceTemplateEntry>> TemplatesByCategory { get; init; } = [];
    }

    private sealed class WellnessServiceTemplateEntry
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public int DefaultDurationMinutes { get; init; }

        public string BookingDeliveryType { get; init; } = "appointment";
    }
}
