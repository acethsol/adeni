CREATE TABLE [subscriptions].[pricing_catalog_entries] (
    [Id]                       UNIQUEIDENTIFIER   NOT NULL,
    [PricingCatalogVersionId]  UNIQUEIDENTIFIER   NOT NULL,
    [CountryCode]              NVARCHAR (2)       NOT NULL,
    [CurrencyCode]             NVARCHAR (3)       NOT NULL,
    [SubscriptionSku]          INT                NOT NULL,
    [MonthlyAmountMajor]       DECIMAL (18, 2)    NULL,
    [PlatformFeePercent]       DECIMAL (5, 2)     NOT NULL,
    [IsSelfServeEnabled]       BIT                NOT NULL,
    CONSTRAINT [PK_pricing_catalog_entries] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_pricing_catalog_entries_version]
        FOREIGN KEY ([PricingCatalogVersionId])
        REFERENCES [subscriptions].[pricing_catalog_versions] ([Id])
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_pricing_catalog_entries_Version_Country_Sku]
    ON [subscriptions].[pricing_catalog_entries]([PricingCatalogVersionId] ASC, [CountryCode] ASC, [SubscriptionSku] ASC);


GO
