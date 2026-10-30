CREATE TABLE [finance].[commerce_requests] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Amount] decimal(12,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [Label] nvarchar(200) NOT NULL,
    [CreatedBy] nvarchar(128) NOT NULL,
    [Nonce] nvarchar(64) NOT NULL,
    [KeyVersion] nvarchar(32) NOT NULL,
    [TokenHash] nvarchar(64) NOT NULL,
    [Purpose] int NOT NULL,
    [State] int NOT NULL,
    [AllowsGiftCard] bit NOT NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [ExpiresAt] datetimeoffset(7) NOT NULL,
    [PaidAt] datetimeoffset(7) NULL,
    [CancelledAt] datetimeoffset(7) NULL,
    [RowVersion] rowversion,
    CONSTRAINT [PK_commerce_requests] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_commerce_requests_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_commerce_requests_tenant] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [CK_commerce_requests_Amount] CHECK ([Amount] > 0),
    CONSTRAINT [CK_commerce_requests_Purpose] CHECK ([Purpose] IN (0,1)),
    CONSTRAINT [CK_commerce_requests_GiftTender] CHECK ([Purpose] = 0 OR [AllowsGiftCard] = 0)
);
GO
CREATE UNIQUE INDEX [IX_commerce_requests_TokenHash] ON [finance].[commerce_requests] ([TokenHash]);
GO
