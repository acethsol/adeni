namespace Adeni.Infrastructure.Notifications;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;

internal static class NotificationTemplateCatalogJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    internal static IReadOnlyDictionary<string, IReadOnlyDictionary<string, TemplateLocaleCopy>> LoadFromDefaultPaths()
    {
        var path = ResolveCatalogPath(null);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Notification template catalog not found at '{path}'. " +
                "Ensure packages/shared/src/data/notification-templates.json is copied to output.");
        }

        return ParseCatalog(File.ReadAllText(path));
    }

    internal static IReadOnlyDictionary<string, IReadOnlyDictionary<string, TemplateLocaleCopy>> Load(
        IHostEnvironment environment)
    {
        var path = ResolveCatalogPath(environment);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Notification template catalog not found at '{path}'. " +
                "Ensure packages/shared/src/data/notification-templates.json is copied to output.");
        }

        return ParseCatalog(File.ReadAllText(path));
    }

    internal static string ResolveCatalogPath(IHostEnvironment? environment)
    {
        var candidates = new List<string>();
        if (environment is not null)
        {
            candidates.Add(Path.Combine(environment.ContentRootPath, "Catalog", "notification-templates.json"));
        }

        candidates.Add(Path.Combine(AppContext.BaseDirectory, "Catalog", "notification-templates.json"));

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, TemplateLocaleCopy>> ParseCatalog(
        string json)
    {
        var document = JsonSerializer.Deserialize<NotificationTemplateCatalogDocument>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Notification template catalog JSON is invalid.");

        return document.Templates.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, TemplateLocaleCopy>)pair.Value.ToDictionary(
                locale => locale.Key,
                locale => new TemplateLocaleCopy(locale.Value.Subject, locale.Value.Body)),
            StringComparer.Ordinal);
    }

    private sealed class NotificationTemplateCatalogDocument
    {
        public Dictionary<string, Dictionary<string, TemplateLocaleCopyJson>> Templates { get; init; } = [];
    }

    private sealed class TemplateLocaleCopyJson
    {
        public string Subject { get; init; } = string.Empty;

        public string Body { get; init; } = string.Empty;
    }
}

internal sealed record TemplateLocaleCopy(string Subject, string Body);
