-- Finance module Phases 2–4 (markets providers, compensation, commission setting)

IF COL_LENGTH('catalog.markets', 'DefaultCommerceProvider') IS NULL
BEGIN
    ALTER TABLE [catalog].[markets]
        ADD [DefaultCommerceProvider] NVARCHAR(16) NOT NULL
            CONSTRAINT [DF_markets_DefaultCommerceProvider] DEFAULT (N'paystack');
END
GO

IF COL_LENGTH('catalog.markets', 'DefaultBillingProvider') IS NULL
BEGIN
    ALTER TABLE [catalog].[markets]
        ADD [DefaultBillingProvider] NVARCHAR(16) NOT NULL
            CONSTRAINT [DF_markets_DefaultBillingProvider] DEFAULT (N'paystack');
END
GO

UPDATE [catalog].[markets]
SET [DefaultCommerceProvider] = N'stripe',
    [DefaultBillingProvider] = N'paystack'
WHERE [Id] = N'ottawa';
GO

UPDATE [catalog].[markets]
SET [DefaultCommerceProvider] = N'paystack',
    [DefaultBillingProvider] = N'paystack'
WHERE [Id] = N'lagos';
GO

IF COL_LENGTH('tenancy.business_profiles', 'StaffCommissionPercent') IS NULL
BEGIN
    ALTER TABLE [tenancy].[business_profiles]
        ADD [StaffCommissionPercent] INT NOT NULL
            CONSTRAINT [DF_business_profiles_StaffCommissionPercent] DEFAULT ((0));
END
GO

IF OBJECT_ID(N'[payments].[staff_compensation_allocations]', N'U') IS NULL
BEGIN
    CREATE TABLE [payments].[staff_compensation_allocations] (
        [Id]               UNIQUEIDENTIFIER   NOT NULL,
        [TenantId]         UNIQUEIDENTIFIER   NOT NULL,
        [StaffMemberId]    UNIQUEIDENTIFIER   NOT NULL,
        [BookingId]        UNIQUEIDENTIFIER   NULL,
        [PaymentIntentId]  UNIQUEIDENTIFIER   NULL,
        [Type]             INT                NOT NULL,
        [Amount]           DECIMAL (12, 2)    NOT NULL,
        [Currency]         NVARCHAR (3)       NOT NULL,
        [CreatedAt]        DATETIMEOFFSET (7) NOT NULL,
        CONSTRAINT [PK_staff_compensation_allocations] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_staff_compensation_allocations_TenantId_CreatedAt]
        ON [payments].[staff_compensation_allocations]([TenantId] ASC, [CreatedAt] ASC);

    CREATE NONCLUSTERED INDEX [IX_staff_compensation_allocations_TenantId_StaffMemberId_CreatedAt]
        ON [payments].[staff_compensation_allocations]([TenantId] ASC, [StaffMemberId] ASC, [CreatedAt] ASC);
END
GO

IF COL_LENGTH('payments.payment_intents', 'TipAmount') IS NULL
BEGIN
    ALTER TABLE [payments].[payment_intents]
        ADD [TipAmount] DECIMAL (12, 2) NULL,
            [TipStaffMemberId] UNIQUEIDENTIFIER NULL;
END
GO
