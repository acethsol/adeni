CREATE TABLE [finance].[gift_cards] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [OriginalAmount] decimal(12,2) NOT NULL,
    [RemainingAmount] decimal(12,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [Nonce] nvarchar(64) NOT NULL,
    [KeyVersion] nvarchar(32) NOT NULL,
    [CodeHash] nvarchar(64) NOT NULL,
    [CodeSuffix] nvarchar(8) NOT NULL,
    [PurchaseRequestId] uniqueidentifier NOT NULL,
    [PurchaserCustomerId] uniqueidentifier NULL,
    [State] int NOT NULL,
    [Sequence] int NOT NULL,
    [IssuedAt] datetimeoffset(7) NULL,
    [RowVersion] rowversion,
    CONSTRAINT [PK_gift_cards] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_gift_cards_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_gift_cards_tenant] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_gift_cards_PurchaseRequestId] FOREIGN KEY ([TenantId],[PurchaseRequestId]) REFERENCES [finance].[commerce_requests] ([TenantId],[Id]),
    CONSTRAINT [CK_gift_cards_Balance] CHECK ([RemainingAmount] >= 0 AND [RemainingAmount] <= [OriginalAmount] AND [OriginalAmount] > 0)
);
GO
CREATE UNIQUE INDEX [IX_gift_cards_TenantId_PurchaseRequestId] ON [finance].[gift_cards] ([TenantId], [PurchaseRequestId]);
GO
CREATE UNIQUE INDEX [IX_gift_cards_TenantId_CodeHash] ON [finance].[gift_cards] ([TenantId], [CodeHash]);
GO
