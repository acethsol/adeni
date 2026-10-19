/*
  Idempotent seed for pricing catalog version 2026-launch-01.
  Aligns with packages/shared/src/data/country-pricing.json.
*/
SET NOCOUNT ON;

DECLARE @VersionId UNIQUEIDENTIFIER = 'a1b2c3d4-e5f6-4789-a012-202601010001';
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

IF NOT EXISTS (SELECT 1 FROM [subscriptions].[pricing_catalog_versions] WHERE [Code] = N'2026-launch-01')
BEGIN
    INSERT INTO [subscriptions].[pricing_catalog_versions] ([Id], [Code], [EffectiveFrom], [EffectiveTo], [Status], [CreatedAt])
    VALUES (@VersionId, N'2026-launch-01', '2026-10-19T00:00:00+00:00', NULL, 1, @Now);

    INSERT INTO [subscriptions].[pricing_catalog_entries]
        ([Id], [PricingCatalogVersionId], [CountryCode], [CurrencyCode], [SubscriptionSku], [MonthlyAmountMajor], [PlatformFeePercent], [IsSelfServeEnabled])
    VALUES
        ('b1000001-0000-4000-8000-000000000001', @VersionId, N'NG', N'NGN', 0, 35000.00, 0, 1),
        ('b1000001-0000-4000-8000-000000000002', @VersionId, N'NG', N'NGN', 1, 55000.00, 0, 1),
        ('b1000001-0000-4000-8000-000000000003', @VersionId, N'NG', N'NGN', 2, 95000.00, 0, 1),
        ('b1000002-0000-4000-8000-000000000001', @VersionId, N'CA', N'CAD', 0, 99.00, 0, 1),
        ('b1000002-0000-4000-8000-000000000002', @VersionId, N'CA', N'CAD', 1, 149.00, 0, 1),
        ('b1000002-0000-4000-8000-000000000003', @VersionId, N'CA', N'CAD', 2, 249.00, 0, 1),
        ('b1000003-0000-4000-8000-000000000001', @VersionId, N'US', N'USD', 0, NULL, 0, 0),
        ('b1000003-0000-4000-8000-000000000002', @VersionId, N'US', N'USD', 1, NULL, 0, 0),
        ('b1000003-0000-4000-8000-000000000003', @VersionId, N'US', N'USD', 2, NULL, 0, 0);
END

GO
