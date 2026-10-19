CREATE TABLE [subscriptions].[pricing_catalog_versions] (
    [Id]            UNIQUEIDENTIFIER   NOT NULL,
    [Code]          NVARCHAR (64)      NOT NULL,
    [EffectiveFrom] DATETIMEOFFSET (7) NOT NULL,
    [EffectiveTo]   DATETIMEOFFSET (7) NULL,
    [Status]        INT                NOT NULL,
    [CreatedAt]     DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_pricing_catalog_versions] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_pricing_catalog_versions_Code]
    ON [subscriptions].[pricing_catalog_versions]([Code] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_pricing_catalog_versions_Status]
    ON [subscriptions].[pricing_catalog_versions]([Status] ASC);


GO
