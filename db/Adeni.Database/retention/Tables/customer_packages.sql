CREATE TABLE [retention].[customer_packages] (
    [Id]                        UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]                  UNIQUEIDENTIFIER   NOT NULL,
    [TenantCustomerId]          UNIQUEIDENTIFIER   NOT NULL,
    [PackageDefinitionId]       UNIQUEIDENTIFIER   NOT NULL,
    [SoldAtLocationId]          UNIQUEIDENTIFIER   NULL,
    [SoldChannel]               INT                NOT NULL,
    [PaymentIntentId]           UNIQUEIDENTIFIER   NULL,
    [Status]                    INT                NOT NULL,
    [NameSnapshot]              NVARCHAR (200)     NOT NULL,
    [ListPriceSnapshot]         DECIMAL (12, 2)    NOT NULL,
    [CurrencySnapshot]          NVARCHAR (3)       NOT NULL,
    [RedemptionScopeSnapshot]   INT                NOT NULL,
    [RedemptionLocationIdsJson] NVARCHAR (4000)    NULL,
    [Note]                      NVARCHAR (500)     NULL,
    [SoldBy]                    NVARCHAR (128)     NOT NULL,
    [CreatedAt]                 DATETIMEOFFSET (7) NOT NULL,
    [ActivatedAt]               DATETIMEOFFSET (7) NULL,
    [ExpiresAt]                 DATETIMEOFFSET (7) NULL,
    [ExpiredAt]                 DATETIMEOFFSET (7) NULL,
    [VoidedAt]                  DATETIMEOFFSET (7) NULL,
    [VoidedBy]                  NVARCHAR (128)     NULL,
    [VoidReason]                NVARCHAR (500)     NULL,
    [RowVersion]                ROWVERSION         NOT NULL,
    CONSTRAINT [PK_customer_packages] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_customer_packages_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_customer_packages_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_customer_packages_tenant_customers] FOREIGN KEY ([TenantId], [TenantCustomerId]) REFERENCES [tenancy].[tenant_customers] ([TenantId], [Id]),
    CONSTRAINT [FK_customer_packages_package_definitions] FOREIGN KEY ([TenantId], [PackageDefinitionId]) REFERENCES [retention].[package_definitions] ([TenantId], [Id]),
    CONSTRAINT [FK_customer_packages_business_locations] FOREIGN KEY ([TenantId], [SoldAtLocationId]) REFERENCES [tenancy].[business_locations] ([TenantId], [Id]),
    CONSTRAINT [CK_customer_packages_Status] CHECK ([Status] IN (0,1,2,3)),
    CONSTRAINT [CK_customer_packages_SoldChannel] CHECK ([SoldChannel] IN (0,1,2)),
    CONSTRAINT [CK_customer_packages_RedemptionScope] CHECK ([RedemptionScopeSnapshot] IN (0,1,2))
);
GO
CREATE NONCLUSTERED INDEX [IX_customer_packages_TenantId_TenantCustomerId_Status]
    ON [retention].[customer_packages] ([TenantId] ASC, [TenantCustomerId] ASC, [Status] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_customer_packages_TenantId_Status_ExpiresAt]
    ON [retention].[customer_packages] ([TenantId] ASC, [Status] ASC, [ExpiresAt] ASC);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_customer_packages_PaymentIntentId]
    ON [retention].[customer_packages] ([PaymentIntentId] ASC) WHERE [PaymentIntentId] IS NOT NULL;
GO
