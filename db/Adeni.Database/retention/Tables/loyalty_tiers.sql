CREATE TABLE [retention].[loyalty_tiers] (
    [Id]                 UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]           UNIQUEIDENTIFIER   NOT NULL,
    [Name]               NVARCHAR (120)     NOT NULL,
    [MinLifetimeEarned]  INT                NOT NULL,
    [SortOrder]          INT                NOT NULL,
    [CreatedAt]          DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]          DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_loyalty_tiers] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_loyalty_tiers_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_loyalty_tiers_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [CK_loyalty_tiers_MinLifetimeEarned] CHECK ([MinLifetimeEarned] >= 0),
    CONSTRAINT [CK_loyalty_tiers_SortOrder] CHECK ([SortOrder] >= 0)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_loyalty_tiers_TenantId_MinLifetimeEarned]
    ON [retention].[loyalty_tiers] ([TenantId] ASC, [MinLifetimeEarned] ASC);
GO
