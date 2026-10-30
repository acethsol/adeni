CREATE TABLE [retention].[referral_codes] (
    [Id]               UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]         UNIQUEIDENTIFIER   NOT NULL,
    [TenantCustomerId] UNIQUEIDENTIFIER   NOT NULL,
    [Code]             NVARCHAR (32)      NOT NULL,
    [CreatedAt]        DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_referral_codes] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_referral_codes_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_referral_codes_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [CK_referral_codes_Code] CHECK (LEN([Code]) BETWEEN 4 AND 32)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_referral_codes_TenantId_TenantCustomerId]
    ON [retention].[referral_codes] ([TenantId] ASC, [TenantCustomerId] ASC);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_referral_codes_TenantId_Code]
    ON [retention].[referral_codes] ([TenantId] ASC, [Code] ASC);
GO
