CREATE TABLE [retention].[loyalty_programs] (
    [Id]                 UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]           UNIQUEIDENTIFIER   NOT NULL,
    [IsEnabled]          BIT                NOT NULL,
    [Currency]           NVARCHAR (3)       NOT NULL,
    [EarnPoints]         INT                NOT NULL,
    [EarnPerAmount]      DECIMAL (12, 2)    NOT NULL,
    [RedeemPoints]       INT                NOT NULL,
    [RedeemValue]        DECIMAL (12, 2)    NOT NULL,
    [MinRedeemPoints]    INT                NOT NULL,
    [PointsExpiryMonths] INT                NULL,
    [UpdatedBy]          NVARCHAR (128)     NOT NULL,
    [CreatedAt]          DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]          DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_loyalty_programs] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_loyalty_programs_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_loyalty_programs_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [CK_loyalty_programs_Earn] CHECK ([EarnPoints] BETWEEN 1 AND 1000 AND [EarnPerAmount] > 0),
    CONSTRAINT [CK_loyalty_programs_Redeem] CHECK ([RedeemPoints] BETWEEN 1 AND 1000000 AND [RedeemValue] > 0),
    CONSTRAINT [CK_loyalty_programs_MinRedeemPoints] CHECK ([MinRedeemPoints] BETWEEN 0 AND 1000000),
    CONSTRAINT [CK_loyalty_programs_PointsExpiryMonths] CHECK ([PointsExpiryMonths] IS NULL OR [PointsExpiryMonths] BETWEEN 1 AND 60)
);
GO
-- One program per tenant.
CREATE UNIQUE NONCLUSTERED INDEX [IX_loyalty_programs_TenantId]
    ON [retention].[loyalty_programs] ([TenantId] ASC);
GO
