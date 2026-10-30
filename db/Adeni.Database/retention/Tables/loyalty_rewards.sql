CREATE TABLE [retention].[loyalty_rewards] (
    [Id]                 UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]           UNIQUEIDENTIFIER   NOT NULL,
    [Name]               NVARCHAR (200)     NOT NULL,
    [Description]        NVARCHAR (500)     NULL,
    [Kind]               INT                NOT NULL,
    [PointsCost]         INT                NOT NULL,
    [ServiceOfferingId]  UNIQUEIDENTIFIER   NULL,
    [IsActive]           BIT                NOT NULL,
    [SortOrder]          INT                NOT NULL,
    [CreatedAt]          DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]          DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_loyalty_rewards] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_loyalty_rewards_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_loyalty_rewards_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [CK_loyalty_rewards_Kind] CHECK ([Kind] IN (0)),
    CONSTRAINT [CK_loyalty_rewards_PointsCost] CHECK ([PointsCost] > 0),
    CONSTRAINT [CK_loyalty_rewards_SortOrder] CHECK ([SortOrder] >= 0)
);
GO
CREATE NONCLUSTERED INDEX [IX_loyalty_rewards_TenantId_IsActive_SortOrder]
    ON [retention].[loyalty_rewards] ([TenantId] ASC, [IsActive] ASC, [SortOrder] ASC);
GO
