namespace Adeni.Infrastructure.Tests.Discovery;

using System.Data;
using Adeni.Infrastructure.Discovery;
using Adeni.Infrastructure.Tests.Persistence;

public sealed class DiscoverySearchExecutorTests
{
    [Fact]
    public async Task SearchAsync_maps_rows_and_total_from_fake_connection()
    {
        var locationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var connection = new FakeDbConnection
        {
            ReaderFactory = command =>
            {
                command.Parameters.Cast<FakeDbParameter>().Single(p => p.ParameterName == "@TotalCount").Value = 7;
                return new FakeDbDataReader(
                [
                    new Dictionary<string, object?>
                    {
                        ["LocationId"] = locationId,
                        ["TenantId"] = tenantId,
                        ["Name"] = "Shop",
                        ["LocationName"] = "Lekki",
                        ["Slug"] = "shop",
                        ["CategorySlug"] = "barbers",
                        ["BusinessType"] = 0,
                        ["Area"] = "Lekki",
                        ["MarketId"] = "lagos",
                        ["CoverImageKey"] = "c.webp",
                        ["GalleryImageKeysJson"] = null,
                        ["DistanceKm"] = 0.5d,
                        ["Latitude"] = 6.4d,
                        ["Longitude"] = 3.4d,
                        ["RatingAvg"] = 5.0d,
                        ["ReviewCount"] = 3,
                    },
                ]);
            },
        };

        var (total, rows) = await DiscoverySearchExecutor.SearchAsync(
            connection,
            transaction: null,
            latitude: 6.4,
            longitude: 3.4,
            categoryCsv: "barbers",
            marketId: "lagos",
            likeQuery: null,
            minRating: null,
            verifiedStatus: 2,
            sort: "distance",
            offset: 0,
            pageSize: 20,
            CancellationToken.None);

        Assert.Equal(7, total);
        Assert.Single(rows);
        Assert.Equal("shop", rows[0].Slug);
        Assert.Equal(CommandType.StoredProcedure, connection.LastCommand!.CommandType);
        Assert.Equal("[tenancy].[DiscoverySearch]", connection.LastCommand.CommandText);
    }

    [Fact]
    public void BuildParameters_includes_output_total()
    {
        var parameters = DiscoverySearchExecutor.BuildParameters(
            1, 2, null, null, null, null, 2, "featured", 0, 10);

        Assert.Contains(parameters, p => p.Name == "@TotalCount" && p.Direction == ParameterDirection.Output);
        Assert.Contains(parameters, p => p.Name == "@Sort" && Equals(p.Value, "featured"));
    }

    [Fact]
    public void MapRow_reads_all_columns_including_nullables()
    {
        var locationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var reader = new FakeDbDataReader(
        [
            new Dictionary<string, object?>
            {
                ["LocationId"] = locationId,
                ["TenantId"] = tenantId,
                ["Name"] = "Lekki Cuts",
                ["LocationName"] = "Lekki",
                ["Slug"] = "lekki-cuts",
                ["CategorySlug"] = "barbers",
                ["BusinessType"] = 0,
                ["Area"] = "Lekki",
                ["MarketId"] = "lagos",
                ["CoverImageKey"] = null,
                ["GalleryImageKeysJson"] = """["a.jpg"]""",
                ["DistanceKm"] = 1.25d,
                ["Latitude"] = 6.45d,
                ["Longitude"] = 3.47d,
                ["RatingAvg"] = null,
                ["ReviewCount"] = 0,
            },
        ]);

        Assert.True(reader.Read());
        var row = DiscoverySearchExecutor.MapRow(reader);

        Assert.Equal(locationId, row.LocationId);
        Assert.Equal(tenantId, row.TenantId);
        Assert.Equal("Lekki Cuts", row.Name);
        Assert.Equal("lekki-cuts", row.Slug);
        Assert.Null(row.CoverImageKey);
        Assert.Equal("""["a.jpg"]""", row.GalleryImageKeysJson);
        Assert.Equal(1.25d, row.DistanceKm);
        Assert.Null(row.RatingAvg);
        Assert.Equal(0, row.ReviewCount);
    }

    [Fact]
    public void MapRow_reads_populated_optional_fields()
    {
        var reader = new FakeDbDataReader(
        [
            new Dictionary<string, object?>
            {
                ["LocationId"] = Guid.NewGuid(),
                ["TenantId"] = Guid.NewGuid(),
                ["Name"] = "VI Salon",
                ["LocationName"] = "VI",
                ["Slug"] = "vi-salon",
                ["CategorySlug"] = "hair-salons",
                ["BusinessType"] = 1,
                ["Area"] = "VI",
                ["MarketId"] = "lagos",
                ["CoverImageKey"] = "cover.webp",
                ["GalleryImageKeysJson"] = null,
                ["DistanceKm"] = 3.5d,
                ["Latitude"] = 6.4d,
                ["Longitude"] = 3.4d,
                ["RatingAvg"] = 4.5d,
                ["ReviewCount"] = 12,
            },
        ]);

        Assert.True(reader.Read());
        var row = DiscoverySearchExecutor.MapRow(reader);

        Assert.Equal("cover.webp", row.CoverImageKey);
        Assert.Null(row.GalleryImageKeysJson);
        Assert.Equal(4.5d, row.RatingAvg);
        Assert.Equal(12, row.ReviewCount);
        Assert.Equal(1, row.BusinessType);
    }
}
