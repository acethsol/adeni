-- Idempotent add of tenant billing columns (existing deployments).
IF COL_LENGTH('tenancy.tenants', 'SubscriptionSku') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [SubscriptionSku] INT NOT NULL CONSTRAINT [DF_tenants_SubscriptionSku] DEFAULT ((0));
END
GO

IF COL_LENGTH('tenancy.tenants', 'BillingStatus') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [BillingStatus] INT NOT NULL CONSTRAINT [DF_tenants_BillingStatus] DEFAULT ((0));
END
GO

IF COL_LENGTH('tenancy.tenants', 'TrialSku') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [TrialSku] INT NULL;
END
GO

IF COL_LENGTH('tenancy.tenants', 'TrialEndsAt') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [TrialEndsAt] DATETIMEOFFSET (7) NULL;
END
GO

IF COL_LENGTH('tenancy.tenants', 'PaidThrough') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [PaidThrough] DATETIMEOFFSET (7) NULL;
END
GO

IF COL_LENGTH('tenancy.tenants', 'DiscountPercent') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [DiscountPercent] DECIMAL (5, 2) NULL;
END
GO

IF COL_LENGTH('tenancy.tenants', 'DiscountFixedAmount') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [DiscountFixedAmount] DECIMAL (18, 2) NULL;
END
GO

IF COL_LENGTH('tenancy.tenants', 'DiscountCurrency') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [DiscountCurrency] NVARCHAR (3) NULL;
END
GO

IF COL_LENGTH('tenancy.tenants', 'DiscountCyclesRemaining') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [DiscountCyclesRemaining] INT NULL;
END
GO

IF COL_LENGTH('tenancy.tenants', 'BillingCompReason') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [BillingCompReason] NVARCHAR (500) NULL;
END
GO

IF COL_LENGTH('tenancy.tenants', 'CustomSaasMonthlyAmount') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [CustomSaasMonthlyAmount] DECIMAL (18, 2) NULL;
END
GO

IF COL_LENGTH('tenancy.tenants', 'CustomSaasCurrency') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [CustomSaasCurrency] NVARCHAR (3) NULL;
END
GO

IF COL_LENGTH('tenancy.tenants', 'CustomSaasSku') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [CustomSaasSku] INT NULL;
END
GO
