CREATE TABLE [tenancy].[business_locations] (
    [Id]          UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]    UNIQUEIDENTIFIER   NOT NULL,
    [Slug]        NVARCHAR (64)      NOT NULL,
    [Name]        NVARCHAR (200)     NOT NULL,
    [MarketId]    NVARCHAR (32)      NOT NULL,
    [AddressLine] NVARCHAR (500)     NOT NULL,
    [Area]        NVARCHAR (120)     NOT NULL,
    [Latitude]    FLOAT (53)         NULL,
    [Longitude]   FLOAT (53)         NULL,
    [TimeZoneId]  NVARCHAR (64)      NULL,
    [IsPrimary]   BIT                NOT NULL,
    [IsActive]    BIT                NOT NULL,
    [CreatedAt]   DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]   DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_business_locations] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_business_locations_business_profiles_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[business_profiles] ([TenantId]) ON DELETE CASCADE,
    CONSTRAINT [FK_business_locations_tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_business_locations_MarketId]
    ON [tenancy].[business_locations]([MarketId] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_business_locations_Slug]
    ON [tenancy].[business_locations]([Slug] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_business_locations_TenantId_IsPrimary]
    ON [tenancy].[business_locations]([TenantId] ASC, [IsPrimary] ASC);


GO

