namespace Adeni.Infrastructure.Subscriptions;

using System.Text.Json;
using Microsoft.Extensions.Hosting;

internal static class PlanEntitlementsCatalogJson
{
    internal static string ResolveCatalogPath(IHostEnvironment environment)
    {
        var candidates = new[]
        {
            Path.Combine(environment.ContentRootPath, "Catalog", "plan-entitlements.json"),
            Path.Combine(AppContext.BaseDirectory, "Catalog", "plan-entitlements.json"),
            Path.Combine(AppContext.BaseDirectory, "plan-entitlements.json"),
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

    internal static JsonElement ReadRoot(IHostEnvironment environment)
    {
        var catalogPath = ResolveCatalogPath(environment);
        if (!File.Exists(catalogPath))
        {
            throw new FileNotFoundException(
                $"Plan entitlements catalog not found at '{catalogPath}'. " +
                "Ensure packages/shared/src/data/plan-entitlements.json is copied to output Catalog/.",
                catalogPath);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        return document.RootElement.Clone();
    }
}
