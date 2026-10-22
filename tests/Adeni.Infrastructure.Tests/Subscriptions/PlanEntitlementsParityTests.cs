namespace Adeni.Infrastructure.Tests.Subscriptions;

using System.Text.Json;
using System.Text.Json.Nodes;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Subscriptions;

public sealed class PlanEntitlementsParityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void Shared_catalog_json_matches_seed_file()
    {
        var shared = ReadCatalogText("plan-entitlements.json");
        var seed = ReadCatalogText("plan-entitlements.seed.json");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(shared), JsonNode.Parse(seed)));
    }

    [Theory]
    [InlineData(SubscriptionSku.Starter, "starter")]
    [InlineData(SubscriptionSku.Professional, "professional")]
    [InlineData(SubscriptionSku.Business, "business")]
    public void PlanEntitlementsCatalog_matches_shared_json(SubscriptionSku sku, string jsonKey)
    {
        var catalog = PlanEntitlementsCatalog.ForSku(sku);
        var row = ReadCatalogJson("plan-entitlements.json").GetProperty(jsonKey);

        Assert.Equal(row.GetProperty("maxLocations").GetInt32(), catalog.MaxLocations);
        Assert.Equal(row.GetProperty("maxPractitioners").GetInt32(), catalog.MaxPractitioners);
        Assert.Equal(row.GetProperty("additionalLocationsAllowed").GetBoolean(), catalog.AdditionalLocationsAllowed);
        Assert.Equal(row.GetProperty("additionalPractitionersAllowed").GetBoolean(), catalog.AdditionalPractitionersAllowed);
        Assert.Equal(row.GetProperty("unlimitedServices").GetBoolean(), catalog.UnlimitedServices);
        Assert.Equal(row.GetProperty("unlimitedBookings").GetBoolean(), catalog.UnlimitedBookings);
        Assert.Equal(row.GetProperty("publicProfile").GetBoolean(), catalog.PublicProfile);
        Assert.Equal(row.GetProperty("discoverListing").GetBoolean(), catalog.DiscoverListing);
        Assert.Equal(row.GetProperty("messaging").GetBoolean(), catalog.Messaging);
        Assert.Equal(row.GetProperty("basicReminders").GetBoolean(), catalog.BasicReminders);
        Assert.Equal(row.GetProperty("configurableReminders").GetBoolean(), catalog.ConfigurableReminders);
        Assert.Equal(row.GetProperty("followUpAutomation").GetBoolean(), catalog.FollowUpAutomation);
        Assert.Equal(row.GetProperty("smsEnabled").GetBoolean(), catalog.SmsEnabled);
        Assert.Equal(row.GetProperty("smsUsageBilling").GetBoolean(), catalog.SmsUsageBilling);
        Assert.Equal(row.GetProperty("basicAnalytics").GetBoolean(), catalog.BasicAnalytics);
        Assert.Equal(row.GetProperty("advancedAnalytics").GetBoolean(), catalog.AdvancedAnalytics);
        Assert.Equal(row.GetProperty("customerRetentionAnalytics").GetBoolean(), catalog.CustomerRetentionAnalytics);
        Assert.Equal(row.GetProperty("staffPerformanceAnalytics").GetBoolean(), catalog.StaffPerformanceAnalytics);
        Assert.Equal(row.GetProperty("basicExports").GetBoolean(), catalog.BasicExports);
        Assert.Equal(row.GetProperty("advancedReports").GetBoolean(), catalog.AdvancedReports);
        Assert.Equal(row.GetProperty("excelExport").GetBoolean(), catalog.ExcelExport);
        Assert.Equal(row.GetProperty("waitlist").GetBoolean(), catalog.Waitlist);
        Assert.Equal(row.GetProperty("multiLocationReporting").GetBoolean(), catalog.MultiLocationReporting);
        Assert.Equal(row.GetProperty("advancedPermissions").GetBoolean(), catalog.AdvancedPermissions);
        Assert.Equal(row.GetProperty("prioritySupport").GetBoolean(), catalog.PrioritySupport);

        Assert.True(catalog.InvoicesReceipts);
        Assert.True(catalog.OnlinePayments);
        Assert.True(catalog.Deposits);
    }

    private static JsonElement ReadCatalogJson(string fileName) =>
        JsonDocument.Parse(ReadCatalogText(fileName)).RootElement;

    private static string ReadCatalogText(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Catalog", fileName);
        return File.ReadAllText(path);
    }
}
