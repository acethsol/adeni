CREATE TABLE [calendar].[selections] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [ConnectionId] UNIQUEIDENTIFIER NOT NULL,
    [ExternalId] NVARCHAR(2048) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ExternalIdHash] NVARCHAR(64) NOT NULL,
    [TimeZoneId] NVARCHAR(128) NOT NULL,
    [IsWriteTarget] BIT NOT NULL,
    CONSTRAINT [PK_calendar_selections] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_calendar_selections_TenantId_Id] UNIQUE ([TenantId],[Id]),
    CONSTRAINT [FK_calendar_selections_ConnectionId] FOREIGN KEY ([TenantId],[ConnectionId]) REFERENCES [calendar].[connections] ([TenantId],[Id])
);
GO
CREATE UNIQUE INDEX [IX_calendar_selections_TenantId_ConnectionId_ExternalIdHash] ON [calendar].[selections] ([TenantId],[ConnectionId],[ExternalIdHash]);
GO
CREATE UNIQUE INDEX [IX_calendar_selections_TenantId_ConnectionId] ON [calendar].[selections] ([TenantId],[ConnectionId]) WHERE [IsWriteTarget] = 1;
