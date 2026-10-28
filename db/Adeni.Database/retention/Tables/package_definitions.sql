CREATE TABLE [retention].[package_definitions] (
    [Id]                    UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]              UNIQUEIDENTIFIER   NOT NULL,
    [Name]                  NVARCHAR (200)     NOT NULL,
    [Description]           NVARCHAR (1000)    NULL,
    [ListPrice]             DECIMAL (12, 2)    NOT NULL,
    [Currency]              NVARCHAR (3)       NOT NULL,
    [IsActive]              BIT                NOT NULL,
    [DefaultExpiryMonths]   INT                NULL,
    [RedemptionScope]       INT                NOT NULL,
    [SellScope]             INT                NOT NULL,
    [RedemptionLocationIdsJson] NVARCHAR (4000) NULL,
    [SellLocationIdsJson]   NVARCHAR (4000)    NULL,
    [CreatedAt]             DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]             DATETIMEOFFSET (7) NOT NULL,
    [DeactivatedAt]         DATETIMEOFFSET (7) NULL,
    CONSTRAINT [PK_package_definitions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_package_definitions_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_package_definitions_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [CK_package_definitions_ListPrice] CHECK ([ListPrice] >= 0),
    CONSTRAINT [CK_package_definitions_ExpiryMonths] CHECK ([DefaultExpiryMonths] IS NULL OR ([DefaultExpiryMonths] BETWEEN 1 AND 120)),
    CONSTRAINT [CK_package_definitions_RedemptionScope] CHECK ([RedemptionScope] IN (0,1,2)),
    CONSTRAINT [CK_package_definitions_SellScope] CHECK ([SellScope] IN (0,1))
);
GO
CREATE NONCLUSTERED INDEX [IX_package_definitions_TenantId_IsActive]
    ON [retention].[package_definitions] ([TenantId] ASC, [IsActive] ASC, [CreatedAt] DESC);
GO
