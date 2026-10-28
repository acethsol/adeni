CREATE TABLE [retention].[benefit_operations] (
    [Id]                     UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]               UNIQUEIDENTIFIER   NOT NULL,
    [KeyHash]                NVARCHAR (64)      NOT NULL,
    [Fingerprint]            NVARCHAR (64)      NOT NULL,
    [Kind]                   NVARCHAR (32)      NOT NULL,
    [Actor]                  NVARCHAR (128)     NOT NULL,
    [ResultCustomerPackageId] UNIQUEIDENTIFIER  NULL,
    [OccurredAt]             DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_benefit_operations] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_benefit_operations_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_benefit_operations_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_benefit_operations_customer_packages] FOREIGN KEY ([TenantId], [ResultCustomerPackageId]) REFERENCES [retention].[customer_packages] ([TenantId], [Id])
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_benefit_operations_TenantId_KeyHash]
    ON [retention].[benefit_operations] ([TenantId] ASC, [KeyHash] ASC);
GO
