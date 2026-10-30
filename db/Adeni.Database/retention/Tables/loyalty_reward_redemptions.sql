CREATE TABLE [retention].[loyalty_reward_redemptions] (
    [Id]                   UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]             UNIQUEIDENTIFIER   NOT NULL,
    [LoyaltyAccountId]     UNIQUEIDENTIFIER   NOT NULL,
    [TenantCustomerId]     UNIQUEIDENTIFIER   NOT NULL,
    [LoyaltyRewardId]      UNIQUEIDENTIFIER   NOT NULL,
    [LoyaltyTransactionId] UNIQUEIDENTIFIER   NOT NULL,
    [PointsSpent]          INT                NOT NULL,
    [IssuedAt]             DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_loyalty_reward_redemptions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_loyalty_reward_redemptions_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_loyalty_reward_redemptions_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_loyalty_reward_redemptions_loyalty_accounts] FOREIGN KEY ([TenantId], [LoyaltyAccountId]) REFERENCES [retention].[loyalty_accounts] ([TenantId], [Id]),
    CONSTRAINT [FK_loyalty_reward_redemptions_loyalty_rewards] FOREIGN KEY ([TenantId], [LoyaltyRewardId]) REFERENCES [retention].[loyalty_rewards] ([TenantId], [Id]),
    CONSTRAINT [FK_loyalty_reward_redemptions_loyalty_transactions] FOREIGN KEY ([TenantId], [LoyaltyTransactionId]) REFERENCES [retention].[loyalty_transactions] ([TenantId], [Id]),
    CONSTRAINT [CK_loyalty_reward_redemptions_PointsSpent] CHECK ([PointsSpent] > 0)
);
GO
