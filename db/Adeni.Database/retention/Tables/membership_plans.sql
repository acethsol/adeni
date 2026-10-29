CREATE TABLE [retention].[membership_plans] (
    [Id]                  UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]            UNIQUEIDENTIFIER   NOT NULL,
    [Name]                NVARCHAR (200)     NOT NULL,
    [Description]         NVARCHAR (1000)    NULL,
    [Price]               DECIMAL (12, 2)    NOT NULL,
    [Currency]            NVARCHAR (3)       NOT NULL,
    [BillingInterval]     INT                NOT NULL,
    [RolloverPolicy]      INT                NOT NULL,
    [CancellationPolicy]  INT                NOT NULL,
    [AllowFreeze]         BIT                NOT NULL,
    [PastDueGraceDays]    INT                NOT NULL,
    [IsActive]            BIT                NOT NULL,
    [CreatedAt]           DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]           DATETIMEOFFSET (7) NOT NULL,
    [DeactivatedAt]       DATETIMEOFFSET (7) NULL,
    CONSTRAINT [PK_membership_plans] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_membership_plans_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_membership_plans_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [CK_membership_plans_Price] CHECK ([Price] > 0),
    CONSTRAINT [CK_membership_plans_BillingInterval] CHECK ([BillingInterval] IN (0,1,2)),
    CONSTRAINT [CK_membership_plans_RolloverPolicy] CHECK ([RolloverPolicy] IN (0,1)),
    CONSTRAINT [CK_membership_plans_CancellationPolicy] CHECK ([CancellationPolicy] IN (0,1)),
    CONSTRAINT [CK_membership_plans_PastDueGraceDays] CHECK ([PastDueGraceDays] BETWEEN 0 AND 30)
);
GO
CREATE NONCLUSTERED INDEX [IX_membership_plans_TenantId_IsActive]
    ON [retention].[membership_plans] ([TenantId] ASC, [IsActive] ASC, [CreatedAt] DESC);
GO
