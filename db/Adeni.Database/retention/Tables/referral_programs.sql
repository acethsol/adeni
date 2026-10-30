CREATE TABLE [retention].[referral_programs] (
    [Id]                       UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]                 UNIQUEIDENTIFIER   NOT NULL,
    [IsEnabled]                BIT                NOT NULL,
    [ReferrerRewardPoints]     INT                NOT NULL,
    [RefereeRewardPoints]      INT                NOT NULL,
    [MaxRewardsPerReferrer]    INT                NULL,
    [UpdatedBy]                NVARCHAR (128)     NOT NULL,
    [CreatedAt]                DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]                DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_referral_programs] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_referral_programs_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_referral_programs_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [CK_referral_programs_ReferrerRewardPoints] CHECK ([ReferrerRewardPoints] BETWEEN 0 AND 1000000),
    CONSTRAINT [CK_referral_programs_RefereeRewardPoints] CHECK ([RefereeRewardPoints] BETWEEN 0 AND 1000000),
    CONSTRAINT [CK_referral_programs_MaxRewardsPerReferrer] CHECK ([MaxRewardsPerReferrer] IS NULL OR [MaxRewardsPerReferrer] >= 0)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_referral_programs_TenantId]
    ON [retention].[referral_programs] ([TenantId] ASC);
GO
