CREATE TABLE [retention].[loyalty_transactions] (
    [Id]               UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]         UNIQUEIDENTIFIER   NOT NULL,
    [LoyaltyAccountId] UNIQUEIDENTIFIER   NOT NULL,
    [TenantCustomerId] UNIQUEIDENTIFIER   NOT NULL,
    [Sequence]         INT                NOT NULL,
    [Kind]             INT                NOT NULL,
    [Points]           INT                NOT NULL,
    [BalanceAfter]     INT                NOT NULL,
    [Reason]           NVARCHAR (500)     NULL,
    [BookingId]        UNIQUEIDENTIFIER   NULL,
    [Actor]            NVARCHAR (128)     NOT NULL,
    [KeyHash]          NVARCHAR (64)      NULL,
    [Fingerprint]      NVARCHAR (64)      NULL,
    [OccurredAt]       DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_loyalty_transactions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_loyalty_transactions_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_loyalty_transactions_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_loyalty_transactions_loyalty_accounts] FOREIGN KEY ([TenantId], [LoyaltyAccountId]) REFERENCES [retention].[loyalty_accounts] ([TenantId], [Id]),
    CONSTRAINT [CK_loyalty_transactions_Kind] CHECK ([Kind] IN (0,1,2,3)),
    CONSTRAINT [CK_loyalty_transactions_Points] CHECK ([Points] <> 0),
    CONSTRAINT [CK_loyalty_transactions_BalanceAfter] CHECK ([BalanceAfter] >= 0)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_loyalty_transactions_TenantId_LoyaltyAccountId_Sequence]
    ON [retention].[loyalty_transactions] ([TenantId] ASC, [LoyaltyAccountId] ASC, [Sequence] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_loyalty_transactions_TenantId_TenantCustomerId_OccurredAt]
    ON [retention].[loyalty_transactions] ([TenantId] ASC, [TenantCustomerId] ASC, [OccurredAt] DESC);
GO
-- Manual-adjust idempotency receipt: one key per tenant (replays cannot double-apply).
CREATE UNIQUE NONCLUSTERED INDEX [IX_loyalty_transactions_TenantId_KeyHash]
    ON [retention].[loyalty_transactions] ([TenantId] ASC, [KeyHash] ASC)
    WHERE [KeyHash] IS NOT NULL;
GO
