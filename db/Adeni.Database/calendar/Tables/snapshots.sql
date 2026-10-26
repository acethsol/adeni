CREATE TABLE [calendar].[snapshots] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [ConnectionId] UNIQUEIDENTIFIER NOT NULL,
    [StartAt] DATETIMEOFFSET(7) NOT NULL,
    [EndAt] DATETIMEOFFSET(7) NOT NULL,
    [SyncedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_calendar_snapshots] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_calendar_snapshots_TenantId_Id] UNIQUE ([TenantId],[Id]),
    CONSTRAINT [FK_calendar_snapshots_ConnectionId] FOREIGN KEY ([TenantId],[ConnectionId]) REFERENCES [calendar].[connections] ([TenantId],[Id]),
    CONSTRAINT [CK_calendar_snapshots_interval] CHECK ([EndAt] > [StartAt])
);
GO
CREATE UNIQUE INDEX [IX_calendar_snapshots_TenantId_ConnectionId_StartAt_EndAt] ON [calendar].[snapshots] ([TenantId],[ConnectionId],[StartAt],[EndAt]);
