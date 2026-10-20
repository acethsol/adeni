-- Idempotent commercial binding columns on tenancy.tenants (Sprint 33b-2).
IF COL_LENGTH('tenancy.tenants', 'AgreedMonthlyAmount') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [AgreedMonthlyAmount] DECIMAL (18, 2) NULL;
END
GO

IF COL_LENGTH('tenancy.tenants', 'AgreedCurrencyCode') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [AgreedCurrencyCode] NVARCHAR (3) NULL;
END
GO

IF COL_LENGTH('tenancy.tenants', 'PricingCatalogVersionCode') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [PricingCatalogVersionCode] NVARCHAR (64) NULL;
END
GO

IF COL_LENGTH('tenancy.tenants', 'PriceLockEndsAt') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [PriceLockEndsAt] DATETIMEOFFSET (7) NULL;
END
GO
