CREATE TABLE [calendar].[conflicts] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [StaffMemberId] UNIQUEIDENTIFIER NOT NULL,
    [BookingId] UNIQUEIDENTIFIER NOT NULL,
    [Category] NVARCHAR(32) NOT NULL,
    [StartAt] DATETIMEOFFSET(7) NOT NULL,
    [EndAt] DATETIMEOFFSET(7) NOT NULL,
    [DetectedAt] DATETIMEOFFSET(7) NOT NULL,
    [AcknowledgedAt] DATETIMEOFFSET(7) NULL,
    [ResolvedAt] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_calendar_conflicts] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_calendar_conflicts_TenantId_Id] UNIQUE ([TenantId],[Id]),
    CONSTRAINT [FK_calendar_conflicts_StaffMemberId] FOREIGN KEY ([TenantId],[StaffMemberId]) REFERENCES [booking].[staff_members] ([TenantId],[Id]),
    CONSTRAINT [FK_calendar_conflicts_BookingId] FOREIGN KEY ([TenantId],[BookingId]) REFERENCES [booking].[bookings] ([TenantId],[Id])
);
GO
CREATE UNIQUE INDEX [IX_calendar_conflicts_TenantId_StaffMemberId_BookingId] ON [calendar].[conflicts] ([TenantId],[StaffMemberId],[BookingId]);
