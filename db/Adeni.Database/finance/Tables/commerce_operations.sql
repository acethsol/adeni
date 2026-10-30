CREATE TABLE [finance].[commerce_operations] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [KeyHash] nvarchar(64) NOT NULL,
    [Fingerprint] nvarchar(64) NOT NULL,
    [Kind] nvarchar(32) NOT NULL,
    [Actor] nvarchar(128) NOT NULL,
    [RequestId] uniqueidentifier NOT NULL,
    [OccurredAt] datetimeoffset(7) NOT NULL,
    CONSTRAINT [PK_commerce_operations] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_commerce_operations_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_commerce_operations_tenant] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_commerce_operations_RequestId] FOREIGN KEY ([TenantId],[RequestId]) REFERENCES [finance].[commerce_requests] ([TenantId],[Id])
);
GO
CREATE UNIQUE INDEX [IX_commerce_operations_TenantId_KeyHash] ON [finance].[commerce_operations] ([TenantId], [KeyHash]);
GO
