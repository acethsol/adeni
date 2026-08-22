namespace Adeni.Infrastructure.Catalog;



using System.Text.Json;

using Adeni.Application.Catalog;

using Adeni.Domain.Tenancy;

using Microsoft.Extensions.Hosting;



internal static class CategoryWorkflowCatalogJson

{

    private static readonly JsonSerializerOptions SerializerOptions = new()

    {

        PropertyNameCaseInsensitive = true,

        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

    };



    internal static CategoryWorkflowCatalog ReadFromFile(IHostEnvironment environment)

    {

        var catalogPath = ResolveCatalogPath(environment);

        if (!File.Exists(catalogPath))

        {

            throw new FileNotFoundException(

                $"Category workflow catalog not found at '{catalogPath}'. Ensure packages/shared/src/data/category-workflows.json is copied to output.");

        }



        var json = File.ReadAllText(catalogPath);

        var document = JsonSerializer.Deserialize<CategoryWorkflowCatalogDocument>(json, SerializerOptions)

            ?? throw new InvalidOperationException("Category workflow catalog JSON is empty or invalid.");



        var categories = document.Categories.ToDictionary(

            pair => pair.Key,

            pair => new CategoryWorkflowEntry

            {

                DefaultBusinessType = ParseBusinessType(pair.Value.DefaultBusinessType),

                Capabilities = pair.Value.Capabilities ?? [],

            },

            StringComparer.OrdinalIgnoreCase);



        var businessTypes = document.BusinessTypes.ToDictionary(

            pair => ParseBusinessType(pair.Key) ?? BusinessType.ScheduledAppointment,

            pair => new BusinessTypeWorkflowEntry

            {

                DiscoveryCta = pair.Value.DiscoveryCta ?? DiscoveryCta.BookNow,

                Capabilities = pair.Value.Capabilities ?? [],

            });



        return new CategoryWorkflowCatalog

        {

            DefaultBusinessType = ParseBusinessType(document.DefaultBusinessType) ?? BusinessType.ScheduledAppointment,

            Categories = categories,

            BusinessTypes = businessTypes,

            AppointmentFallbackCapabilities = document.AppointmentFallbackCapabilities ?? [],

        };

    }



    internal static string ResolveCatalogPath(IHostEnvironment environment)

    {

        var candidates = new[]

        {

            Path.Combine(environment.ContentRootPath, "Catalog", "category-workflows.json"),

            Path.Combine(AppContext.BaseDirectory, "Catalog", "category-workflows.json"),

            Path.Combine(AppContext.BaseDirectory, "category-workflows.json"),

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



    private static BusinessType? ParseBusinessType(string? value) =>

        string.Equals(value, "quote_request", StringComparison.OrdinalIgnoreCase)

            ? BusinessType.QuoteRequest

            : string.Equals(value, "scheduled_appointment", StringComparison.OrdinalIgnoreCase)

                ? BusinessType.ScheduledAppointment

                : null;



    private sealed class CategoryWorkflowCatalogDocument

    {

        public string? DefaultBusinessType { get; init; }



        public Dictionary<string, CategoryWorkflowEntryDocument> Categories { get; init; } = new(StringComparer.OrdinalIgnoreCase);



        public Dictionary<string, BusinessTypeWorkflowEntryDocument> BusinessTypes { get; init; } = new(StringComparer.OrdinalIgnoreCase);



        public IReadOnlyList<string>? AppointmentFallbackCapabilities { get; init; }

    }



    private sealed class CategoryWorkflowEntryDocument

    {

        public string? DefaultBusinessType { get; init; }



        public IReadOnlyList<string>? Capabilities { get; init; }

    }



    private sealed class BusinessTypeWorkflowEntryDocument

    {

        public string? DiscoveryCta { get; init; }



        public IReadOnlyList<string>? Capabilities { get; init; }

    }

}

