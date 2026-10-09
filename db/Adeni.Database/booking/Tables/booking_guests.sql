CREATE TABLE [booking].[booking_guests] (
    [Id]          UNIQUEIDENTIFIER NOT NULL,
    [BookingId]   UNIQUEIDENTIFIER NOT NULL,
    [TenantId]    UNIQUEIDENTIFIER NOT NULL,
    [DisplayName] NVARCHAR (120)   NULL,
    [SortOrder]   INT              NOT NULL,
    CONSTRAINT [PK_booking_guests] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_booking_guests_bookings_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [booking].[bookings] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_booking_guests_BookingId_SortOrder]
    ON [booking].[booking_guests]([BookingId] ASC, [SortOrder] ASC);


GO

