CREATE TABLE [calendar].[busy_blocks] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [SnapshotId] UNIQUEIDENTIFIER NOT NULL,
    [StartAt] DATETIMEOFFSET(7) NOT NULL,
    [EndAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_calendar_busy_blocks] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_calendar_busy_blocks_TenantId_Id] UNIQUE ([TenantId],[Id]),
    CONSTRAINT [FK_calendar_busy_blocks_SnapshotId] FOREIGN KEY ([TenantId],[SnapshotId]) REFERENCES [calendar].[snapshots] ([TenantId],[Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_calendar_busy_interval] CHECK ([EndAt] > [StartAt])
);
GO
CREATE INDEX [IX_calendar_busy_blocks_TenantId_SnapshotId_StartAt] ON [calendar].[busy_blocks] ([TenantId],[SnapshotId],[StartAt]);
