CREATE TABLE [finance].[commerce_attempts] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Provider] nvarchar(32) NOT NULL,
    [MerchantReference] nvarchar(128) NOT NULL,
    [CheckoutUrl] nvarchar(2048) NULL,
    [State] nvarchar(32) NOT NULL,
    [RequestId] uniqueidentifier NOT NULL,
    [PaymentIntentId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset(7) NOT NULL,
    [UpdatedAt] datetimeoffset(7) NOT NULL,
    [RowVersion] rowversion,
    CONSTRAINT [PK_commerce_attempts] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_commerce_attempts_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_commerce_attempts_tenant] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_commerce_attempts_RequestId] FOREIGN KEY ([TenantId],[RequestId]) REFERENCES [finance].[commerce_requests] ([TenantId],[Id]),
    CONSTRAINT [FK_commerce_attempts_intent] FOREIGN KEY ([TenantId],[PaymentIntentId]) REFERENCES [finance].[payment_intents] ([TenantId],[Id])
);
GO
CREATE UNIQUE INDEX [IX_commerce_attempts_TenantId_RequestId] ON [finance].[commerce_attempts] ([TenantId], [RequestId]);
GO
CREATE UNIQUE INDEX [IX_commerce_attempts_PaymentIntentId] ON [finance].[commerce_attempts] ([PaymentIntentId]);
GO
