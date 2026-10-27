CREATE TABLE [tenancy].[tenants] (
    [Id]                       UNIQUEIDENTIFIER   NOT NULL,
    [Name]                     NVARCHAR (200)     NOT NULL,
    [Status]                   INT                NOT NULL,
    [SubscriptionSku]          INT                NOT NULL CONSTRAINT [DF_tenants_SubscriptionSku] DEFAULT ((0)),
    [BillingStatus]            INT                NOT NULL CONSTRAINT [DF_tenants_BillingStatus] DEFAULT ((0)),
    [TrialSku]                 INT                NULL,
    [TrialEndsAt]              DATETIMEOFFSET (7) NULL,
    [PaidThrough]              DATETIMEOFFSET (7) NULL,
    [DiscountPercent]          DECIMAL (5, 2)     NULL,
    [DiscountFixedAmount]      DECIMAL (18, 2)    NULL,
    [DiscountCurrency]         NVARCHAR (3)       NULL,
    [DiscountCyclesRemaining]  INT                NULL,
    [BillingCompReason]        NVARCHAR (500)     NULL,
    [CustomSaasMonthlyAmount]  DECIMAL (18, 2)    NULL,
    [CustomSaasCurrency]       NVARCHAR (3)       NULL,
    [CustomSaasSku]            INT                NULL,
    [AgreedMonthlyAmount]      DECIMAL (18, 2)    NULL,
    [AgreedCurrencyCode]       NVARCHAR (3)       NULL,
    [PricingCatalogVersionCode] NVARCHAR (64)     NULL,
    [PriceLockEndsAt]          DATETIMEOFFSET (7) NULL,
    [CreatedAt]                DATETIMEOFFSET (7) NOT NULL,
    [VerifiedAt]               DATETIMEOFFSET (7) NULL,
    [VerificationRejectionReason] NVARCHAR (2000) NULL,
    CONSTRAINT [PK_tenants] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
