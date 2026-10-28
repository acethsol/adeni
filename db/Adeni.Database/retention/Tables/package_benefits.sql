CREATE TABLE [retention].[package_benefits] (
    [Id]                  UNIQUEIDENTIFIER NOT NULL,
    [TenantId]            UNIQUEIDENTIFIER NOT NULL,
    [PackageDefinitionId] UNIQUEIDENTIFIER NOT NULL,
    [ServiceOfferingId]   UNIQUEIDENTIFIER NOT NULL,
    [ServiceName]         NVARCHAR (200)   NOT NULL,
    [QuantityGranted]     INT              NOT NULL,
    [SortOrder]           INT              NOT NULL,
    CONSTRAINT [PK_package_benefits] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_package_benefits_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_package_benefits_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_package_benefits_package_definitions] FOREIGN KEY ([TenantId], [PackageDefinitionId]) REFERENCES [retention].[package_definitions] ([TenantId], [Id]),
    CONSTRAINT [FK_package_benefits_service_offerings] FOREIGN KEY ([TenantId], [ServiceOfferingId]) REFERENCES [booking].[service_offerings] ([TenantId], [Id]),
    CONSTRAINT [CK_package_benefits_Quantity] CHECK ([QuantityGranted] BETWEEN 1 AND 1000)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_package_benefits_TenantId_PackageDefinitionId_ServiceOfferingId]
    ON [retention].[package_benefits] ([TenantId] ASC, [PackageDefinitionId] ASC, [ServiceOfferingId] ASC);
GO
