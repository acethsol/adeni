namespace Adeni.Infrastructure.Discovery;

using System.Data;
using System.Data.Common;
using Adeni.Infrastructure.Persistence;

/// <summary>
/// Domain-specific call to <c>tenancy.DiscoverySearch</c>.
/// Shared ADO plumbing lives in <see cref="StoredProcedureExecutor"/>.
/// </summary>
internal static class DiscoverySearchExecutor
{
    internal static async Task<(int TotalCount, List<DiscoverySearchRow> Rows)> SearchAsync(
        DbConnection connection,
        DbTransaction? transaction,
        double latitude,
        double longitude,
        string? categoryCsv,
        string? marketId,
        string? likeQuery,
        int? minRating,
        int verifiedStatus,
        string sort,
        int offset,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var result = await StoredProcedureExecutor.QueryAsync(
            connection,
            transaction,
            "[tenancy].[DiscoverySearch]",
            MapRow,
            BuildParameters(
                latitude,
                longitude,
                categoryCsv,
                marketId,
                likeQuery,
                minRating,
                verifiedStatus,
                sort,
                offset,
                pageSize),
            cancellationToken);

        return (result.GetOutput<int>("@TotalCount"), result.Rows.ToList());
    }

    internal static SqlParam[] BuildParameters(
        double latitude,
        double longitude,
        string? categoryCsv,
        string? marketId,
        string? likeQuery,
        int? minRating,
        int verifiedStatus,
        string sort,
        int offset,
        int pageSize) =>
    [
        SqlParam.In("@Latitude", latitude),
        SqlParam.In("@Longitude", longitude),
        SqlParam.In("@CategoryCsv", categoryCsv),
        SqlParam.In("@MarketId", marketId),
        SqlParam.In("@LikeQuery", likeQuery),
        SqlParam.In("@MinRating", minRating),
        SqlParam.In("@VerifiedStatus", verifiedStatus),
        SqlParam.In("@Sort", sort),
        SqlParam.In("@Offset", offset),
        SqlParam.In("@PageSize", pageSize),
        SqlParam.Out("@TotalCount", DbType.Int32),
    ];

    internal static DiscoverySearchRow MapRow(DbDataReader reader) =>
        new()
        {
            LocationId = reader.GetGuid(reader.GetOrdinal("LocationId")),
            TenantId = reader.GetGuid(reader.GetOrdinal("TenantId")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            LocationName = reader.GetString(reader.GetOrdinal("LocationName")),
            Slug = reader.GetString(reader.GetOrdinal("Slug")),
            CategorySlug = reader.GetString(reader.GetOrdinal("CategorySlug")),
            BusinessType = reader.GetInt32(reader.GetOrdinal("BusinessType")),
            Area = reader.GetString(reader.GetOrdinal("Area")),
            MarketId = reader.GetString(reader.GetOrdinal("MarketId")),
            CoverImageKey = reader.IsDBNull(reader.GetOrdinal("CoverImageKey"))
                ? null
                : reader.GetString(reader.GetOrdinal("CoverImageKey")),
            GalleryImageKeysJson = reader.IsDBNull(reader.GetOrdinal("GalleryImageKeysJson"))
                ? null
                : reader.GetString(reader.GetOrdinal("GalleryImageKeysJson")),
            DistanceKm = reader.GetDouble(reader.GetOrdinal("DistanceKm")),
            Latitude = reader.GetDouble(reader.GetOrdinal("Latitude")),
            Longitude = reader.GetDouble(reader.GetOrdinal("Longitude")),
            RatingAvg = reader.IsDBNull(reader.GetOrdinal("RatingAvg"))
                ? null
                : reader.GetDouble(reader.GetOrdinal("RatingAvg")),
            ReviewCount = reader.GetInt32(reader.GetOrdinal("ReviewCount")),
        };
}
