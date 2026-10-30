CREATE TABLE [retention].[loyalty_accounts] (
    [Id]               UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]         UNIQUEIDENTIFIER   NOT NULL,
    [TenantCustomerId] UNIQUEIDENTIFIER   NOT NULL,
    [PointsBalance]    INT                NOT NULL,
    [LifetimeEarned]   INT                NOT NULL,
    [LifetimeRedeemed] INT                NOT NULL,
    [Sequence]         INT                NOT NULL,
    [CurrentTierId]    UNIQUEIDENTIFIER   NULL,
    [RowVersion]       ROWVERSION         NOT NULL,
    [CreatedAt]        DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]        DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_loyalty_accounts] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_loyalty_accounts_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_loyalty_accounts_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_loyalty_accounts_tenant_customers] FOREIGN KEY ([TenantId], [TenantCustomerId]) REFERENCES [tenancy].[tenant_customers] ([TenantId], [Id]),
    CONSTRAINT [CK_loyalty_accounts_Invariant] CHECK ([PointsBalance] >= 0 AND [LifetimeEarned] >= 0 AND [LifetimeRedeemed] >= 0 AND [Sequence] >= 0)
);
GO
-- One account per roster customer.
CREATE UNIQUE NONCLUSTERED INDEX [IX_loyalty_accounts_TenantId_TenantCustomerId]
    ON [retention].[loyalty_accounts] ([TenantId] ASC, [TenantCustomerId] ASC);
GO
