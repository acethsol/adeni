CREATE TABLE [finance].[commerce_settlements] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Amount] decimal(12,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [RequestId] uniqueidentifier NOT NULL,
    [PaymentIntentId] uniqueidentifier NULL,
    [GiftTransactionId] uniqueidentifier NULL,
    [SettledAt] datetimeoffset(7) NOT NULL,
    [ReversedAt] datetimeoffset(7) NULL,
    [RowVersion] rowversion,
    CONSTRAINT [PK_commerce_settlements] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_commerce_settlements_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_commerce_settlements_tenant] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_commerce_settlements_RequestId] FOREIGN KEY ([TenantId],[RequestId]) REFERENCES [finance].[commerce_requests] ([TenantId],[Id]),
    CONSTRAINT [FK_commerce_settlements_GiftTransactionId] FOREIGN KEY ([TenantId],[GiftTransactionId]) REFERENCES [finance].[gift_transactions] ([TenantId],[Id]),
    CONSTRAINT [FK_commerce_settlements_intent] FOREIGN KEY ([TenantId],[PaymentIntentId]) REFERENCES [finance].[payment_intents] ([TenantId],[Id]),
    CONSTRAINT [CK_commerce_settlements_Amount] CHECK ([Amount] > 0),
    CONSTRAINT [CK_commerce_settlements_Tender] CHECK (([PaymentIntentId] IS NULL AND [GiftTransactionId] IS NOT NULL) OR ([PaymentIntentId] IS NOT NULL AND [GiftTransactionId] IS NULL))
);
GO
CREATE UNIQUE INDEX [IX_commerce_settlements_TenantId_RequestId] ON [finance].[commerce_settlements] ([TenantId], [RequestId]);
GO
CREATE UNIQUE INDEX [IX_commerce_settlements_PaymentIntentId] ON [finance].[commerce_settlements] ([PaymentIntentId]) WHERE [PaymentIntentId] IS NOT NULL;
GO
CREATE UNIQUE INDEX [IX_commerce_settlements_GiftTransactionId] ON [finance].[commerce_settlements] ([GiftTransactionId]) WHERE [GiftTransactionId] IS NOT NULL;
GO
