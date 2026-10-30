CREATE TABLE [retention].[referral_attributions] (
    [Id]                       UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]                 UNIQUEIDENTIFIER   NOT NULL,
    [ReferrerTenantCustomerId] UNIQUEIDENTIFIER   NOT NULL,
    [RefereeTenantCustomerId]  UNIQUEIDENTIFIER   NOT NULL,
    [ReferralCodeId]           UNIQUEIDENTIFIER   NOT NULL,
    [SourceBookingId]          UNIQUEIDENTIFIER   NULL,
    [QualifiedBookingId]       UNIQUEIDENTIFIER   NULL,
    [Status]                   INT                NOT NULL,
    [RejectReason]             NVARCHAR (64)      NULL,
    [CreatedAt]                DATETIMEOFFSET (7) NOT NULL,
    [QualifiedAt]              DATETIMEOFFSET (7) NULL,
    [RewardedAt]               DATETIMEOFFSET (7) NULL,
    CONSTRAINT [PK_referral_attributions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_referral_attributions_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_referral_attributions_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_referral_attributions_referral_codes] FOREIGN KEY ([TenantId], [ReferralCodeId]) REFERENCES [retention].[referral_codes] ([TenantId], [Id]),
    CONSTRAINT [CK_referral_attributions_Status] CHECK ([Status] IN (0, 1, 2, 3))
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_referral_attributions_TenantId_RefereeTenantCustomerId]
    ON [retention].[referral_attributions] ([TenantId] ASC, [RefereeTenantCustomerId] ASC);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_referral_attributions_TenantId_QualifiedBookingId]
    ON [retention].[referral_attributions] ([TenantId] ASC, [QualifiedBookingId] ASC)
    WHERE [QualifiedBookingId] IS NOT NULL;
GO
