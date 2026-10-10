/*
================================================================================
Procedure : [tenancy].[DiscoverySearch]
Purpose   : Public discovery search — verified locations near a point, with
            category/market/text/rating filters, distance or featured sort,
            and total count for paging.
Caller    : Adeni.Infrastructure.Discovery.DiscoverySearchExecutor
            (via StoredProcedureExecutor — do not embed this SQL in C#)

Parameters
  @Latitude / @Longitude  Search origin (WGS84 degrees)
  @CategoryCsv            Optional comma-separated category slugs (match primary
                          or business_profile_categories)
  @MarketId               Optional market id (e.g. lagos); compared case-insensitive
  @LikeQuery              Optional space-separated tokens (each matched with LIKE
                          %token% across name/area/category/description; ALL
                          tokens must match). Legacy single '%phrase%' still works.
  @MinRating              Optional minimum average rating (1–5)
  @VerifiedStatus         Tenant status enum value (Verified = 2)
  @Sort                   N'featured' | N'distance'
  @Offset / @PageSize     OFFSET / FETCH paging
  @TotalCount             OUTPUT — rows matching filters (before page)

Notes
  - CTEs OK for the filter insert; #candidates holds rows for COUNT + page SELECT.
  - For @Sort = N'distance', RatingAvg/ReviewCount are left empty for the app to fill.

Sample call
--------------------------------------------------------------------------------
DECLARE @TotalCount INT;

EXEC [tenancy].[DiscoverySearch]
    @Latitude       = 6.5244,
    @Longitude      = 3.3792,
    @CategoryCsv    = NULL,              -- or N'hair-salons,barbers'
    @MarketId       = N'lagos',
    @LikeQuery      = NULL,              -- or N'%ikeja%'
    @MinRating      = NULL,
    @VerifiedStatus = 2,                 -- TenantStatus.Verified
    @Sort           = N'distance',       -- or N'featured'
    @Offset         = 0,
    @PageSize       = 20,
    @TotalCount     = @TotalCount OUTPUT;

SELECT @TotalCount AS TotalCount;
-- Result set: LocationId, TenantId, Name, LocationName, Slug, CategorySlug,
-- BusinessType, Area, MarketId, CoverImageKey, GalleryImageKeysJson,
-- DistanceKm, Latitude, Longitude, RatingAvg, ReviewCount
================================================================================
*/
CREATE PROCEDURE [tenancy].[DiscoverySearch]
    @Latitude       FLOAT,
    @Longitude      FLOAT,
    @CategoryCsv    NVARCHAR (MAX)  = NULL,
    @MarketId       NVARCHAR (32)   = NULL,
    @LikeQuery      NVARCHAR (200)  = NULL,
    @MinRating      INT             = NULL,
    @VerifiedStatus INT,
    @Sort           NVARCHAR (20), -- featured | distance
    @Offset         INT,
    @PageSize       INT,
    @TotalCount     INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @MarketIdLower NVARCHAR (32) =
        CASE WHEN @MarketId IS NULL THEN NULL ELSE LOWER(@MarketId) END;

    -- Temp table: CTEs cannot span COUNT + paged SELECT; materialize once, reuse.
    CREATE TABLE [#candidates] (
        [LocationId]           UNIQUEIDENTIFIER NOT NULL,
        [TenantId]             UNIQUEIDENTIFIER NOT NULL,
        [Name]                 NVARCHAR (200)   NOT NULL,
        [LocationName]         NVARCHAR (200)   NOT NULL,
        [Slug]                 NVARCHAR (64)    NOT NULL,
        [CategorySlug]         NVARCHAR (64)    NOT NULL,
        [BusinessType]         INT              NOT NULL,
        [Area]                 NVARCHAR (120)   NOT NULL,
        [MarketId]             NVARCHAR (32)    NOT NULL,
        [CoverImageKey]        NVARCHAR (512)   NULL,
        [GalleryImageKeysJson] NVARCHAR (4000)  NULL,
        [Latitude]             FLOAT            NOT NULL,
        [Longitude]            FLOAT            NOT NULL,
        [DistanceKm]           FLOAT            NOT NULL,
        [RatingAvg]            FLOAT            NULL,
        [ReviewCount]          INT              NOT NULL
    );

    ;WITH [rating_summary] AS (
        SELECT
            r.[TenantId] AS [tenant_id],
            CAST(ROUND(AVG(CAST(r.[Rating] AS FLOAT)), 1) AS FLOAT) AS [rating_avg],
            CAST(COUNT(*) AS INT) AS [review_count]
        FROM [booking].[reviews] AS r
        WHERE r.[IsHidden] = 0
        GROUP BY r.[TenantId]
    ),
    [filtered] AS (
        SELECT
            bl.[Id] AS [LocationId],
            bl.[TenantId] AS [TenantId],
            t.[Name] AS [Name],
            bl.[Name] AS [LocationName],
            bl.[Slug] AS [Slug],
            bp.[CategorySlug] AS [CategorySlug],
            bp.[BusinessType] AS [BusinessType],
            bl.[Area] AS [Area],
            bl.[MarketId] AS [MarketId],
            bp.[CoverImageKey] AS [CoverImageKey],
            bp.[GalleryImageKeysJson] AS [GalleryImageKeysJson],
            bl.[Latitude] AS [Latitude],
            bl.[Longitude] AS [Longitude],
            CAST(ROUND(
                (6371.0 * 2 * ASIN(SQRT(
                    POWER(SIN(RADIANS(bl.[Latitude] - @Latitude) / 2.0), 2) +
                    COS(RADIANS(@Latitude)) * COS(RADIANS(bl.[Latitude])) *
                    POWER(SIN(RADIANS(bl.[Longitude] - @Longitude) / 2.0), 2)
                ))),
                2
            ) AS FLOAT) AS [DistanceKm],
            rs.[rating_avg] AS [RatingAvg],
            COALESCE(rs.[review_count], 0) AS [ReviewCount]
        FROM [tenancy].[business_locations] AS bl
        INNER JOIN [tenancy].[tenants] AS t ON t.[Id] = bl.[TenantId]
        INNER JOIN [tenancy].[business_profiles] AS bp ON bp.[TenantId] = t.[Id]
        LEFT JOIN [rating_summary] AS rs ON rs.[tenant_id] = t.[Id]
        WHERE bl.[IsActive] = 1
          AND bl.[Latitude] IS NOT NULL
          AND bl.[Longitude] IS NOT NULL
          AND t.[Status] = @VerifiedStatus
          AND (
                @CategoryCsv IS NULL
                OR LOWER(bp.[CategorySlug]) IN (
                    SELECT LOWER(LTRIM(RTRIM([value])))
                    FROM STRING_SPLIT(@CategoryCsv, ',')
                )
                OR EXISTS (
                    SELECT 1
                    FROM [tenancy].[business_profile_categories] AS bpc
                    WHERE bpc.[TenantId] = bp.[TenantId]
                      AND LOWER(bpc.[CategorySlug]) IN (
                          SELECT LOWER(LTRIM(RTRIM([value])))
                          FROM STRING_SPLIT(@CategoryCsv, ',')
                      )
                )
              )
          AND (@MarketIdLower IS NULL OR LOWER(bl.[MarketId]) = @MarketIdLower)
          AND (@MinRating IS NULL OR COALESCE(rs.[rating_avg], 0) >= @MinRating)
          AND (
                @LikeQuery IS NULL
                OR (
                    -- Legacy: caller passed a single already-wrapped pattern.
                    LEFT(@LikeQuery, 1) = N'%'
                    AND (
                        LOWER(t.[Name]) LIKE @LikeQuery
                        OR LOWER(bl.[Name]) LIKE @LikeQuery
                        OR LOWER(bl.[Area]) LIKE @LikeQuery
                        OR LOWER(bp.[CategorySlug]) LIKE @LikeQuery
                        OR LOWER(bp.[Description]) LIKE @LikeQuery
                    )
                )
                OR (
                    LEFT(@LikeQuery, 1) <> N'%'
                    AND NOT EXISTS (
                        SELECT 1
                        FROM STRING_SPLIT(@LikeQuery, N' ') AS tok
                        WHERE LEN(LTRIM(RTRIM(tok.[value]))) > 0
                          AND NOT (
                              LOWER(t.[Name]) LIKE N'%' + LOWER(LTRIM(RTRIM(tok.[value]))) + N'%'
                              OR LOWER(bl.[Name]) LIKE N'%' + LOWER(LTRIM(RTRIM(tok.[value]))) + N'%'
                              OR LOWER(bl.[Area]) LIKE N'%' + LOWER(LTRIM(RTRIM(tok.[value]))) + N'%'
                              OR LOWER(bp.[CategorySlug]) LIKE N'%' + LOWER(LTRIM(RTRIM(tok.[value]))) + N'%'
                              OR LOWER(bp.[Description]) LIKE N'%' + LOWER(LTRIM(RTRIM(tok.[value]))) + N'%'
                          )
                    )
                )
              )
    )
    INSERT INTO [#candidates] (
        [LocationId],
        [TenantId],
        [Name],
        [LocationName],
        [Slug],
        [CategorySlug],
        [BusinessType],
        [Area],
        [MarketId],
        [CoverImageKey],
        [GalleryImageKeysJson],
        [Latitude],
        [Longitude],
        [DistanceKm],
        [RatingAvg],
        [ReviewCount]
    )
    SELECT
        [LocationId],
        [TenantId],
        [Name],
        [LocationName],
        [Slug],
        [CategorySlug],
        [BusinessType],
        [Area],
        [MarketId],
        [CoverImageKey],
        [GalleryImageKeysJson],
        [Latitude],
        [Longitude],
        [DistanceKm],
        [RatingAvg],
        [ReviewCount]
    FROM [filtered];

    SELECT @TotalCount = COUNT(*) FROM [#candidates];

    IF @Sort = N'featured'
    BEGIN
        SELECT
            [LocationId],
            [TenantId],
            [Name],
            [LocationName],
            [Slug],
            [CategorySlug],
            [BusinessType],
            [Area],
            [MarketId],
            [CoverImageKey],
            [GalleryImageKeysJson],
            [DistanceKm],
            [Latitude],
            [Longitude],
            [RatingAvg],
            [ReviewCount]
        FROM [#candidates]
        ORDER BY
            COALESCE([RatingAvg], 0) DESC,
            [ReviewCount] DESC,
            [DistanceKm] ASC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
    END
    ELSE
    BEGIN
        -- distance: ratings filled by application after page load
        SELECT
            [LocationId],
            [TenantId],
            [Name],
            [LocationName],
            [Slug],
            [CategorySlug],
            [BusinessType],
            [Area],
            [MarketId],
            [CoverImageKey],
            [GalleryImageKeysJson],
            [DistanceKm],
            [Latitude],
            [Longitude],
            CAST(NULL AS FLOAT) AS [RatingAvg],
            0 AS [ReviewCount]
        FROM [#candidates]
        ORDER BY [DistanceKm] ASC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
    END
END
GO
