CREATE TABLE [finance].[gift_transactions] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Delta] decimal(12,2) NOT NULL,
    [BalanceAfter] decimal(12,2) NOT NULL,
    [Actor] nvarchar(128) NOT NULL,
    [GiftCardId] uniqueidentifier NOT NULL,
    [RequestId] uniqueidentifier NOT NULL,
    [ReversesTransactionId] uniqueidentifier NULL,
    [Sequence] int NOT NULL,
    [Kind] int NOT NULL,
    [OccurredAt] datetimeoffset(7) NOT NULL,
    CONSTRAINT [PK_gift_transactions] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_gift_transactions_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_gift_transactions_tenant] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_gift_transactions_GiftCardId] FOREIGN KEY ([TenantId],[GiftCardId]) REFERENCES [finance].[gift_cards] ([TenantId],[Id]),
    CONSTRAINT [FK_gift_transactions_RequestId] FOREIGN KEY ([TenantId],[RequestId]) REFERENCES [finance].[commerce_requests] ([TenantId],[Id]),
    CONSTRAINT [FK_gift_transactions_ReversesTransactionId] FOREIGN KEY ([TenantId],[ReversesTransactionId]) REFERENCES [finance].[gift_transactions] ([TenantId],[Id]),
    CONSTRAINT [CK_gift_transactions_Balance] CHECK ([BalanceAfter] >= 0),
    CONSTRAINT [CK_gift_transactions_Delta] CHECK (([Kind] IN (0,2) AND [Delta] > 0) OR ([Kind] IN (1,3) AND [Delta] < 0))
);
GO
CREATE UNIQUE INDEX [IX_gift_transactions_TenantId_GiftCardId_Sequence] ON [finance].[gift_transactions] ([TenantId], [GiftCardId], [Sequence]);
GO
CREATE UNIQUE INDEX [IX_gift_transactions_TenantId_ReversesTransactionId] ON [finance].[gift_transactions] ([TenantId], [ReversesTransactionId]) WHERE [ReversesTransactionId] IS NOT NULL;
GO

CREATE UNIQUE INDEX [IX_gift_transactions_IssueOnce] ON [finance].[gift_transactions] ([TenantId],[GiftCardId]) WHERE [Kind] = 0;
GO
