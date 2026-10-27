CREATE TABLE [calendar].[booking_events] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [ConnectionId] UNIQUEIDENTIFIER NOT NULL,
    [BookingId] UNIQUEIDENTIFIER NOT NULL,
    [TargetId] NVARCHAR(2048) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [ExternalId] NVARCHAR(2048) COLLATE Latin1_General_100_BIN2 NULL,
    [AppliedFingerprint] NVARCHAR(64) NULL,
    [State] NVARCHAR(32) NOT NULL,
    [RowVersion] ROWVERSION NOT NULL,
    CONSTRAINT [PK_calendar_booking_events] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_calendar_booking_events_TenantId_Id] UNIQUE ([TenantId],[Id]),
    CONSTRAINT [FK_calendar_booking_events_ConnectionId] FOREIGN KEY ([TenantId],[ConnectionId]) REFERENCES [calendar].[connections] ([TenantId],[Id]),
    CONSTRAINT [FK_calendar_booking_events_BookingId] FOREIGN KEY ([TenantId],[BookingId]) REFERENCES [booking].[bookings] ([TenantId],[Id])
);
GO
CREATE UNIQUE INDEX [IX_calendar_booking_events_TenantId_ConnectionId_BookingId] ON [calendar].[booking_events] ([TenantId],[ConnectionId],[BookingId]);
